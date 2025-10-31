using Atlas.Tracing;
using Microsoft.Extensions.Options;
using NotificationService.Data;
using NotificationService.Models;
using NotificationService.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace NotificationService.Consumers;




public class OrderCancelledConsumer : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly RabbitMQSettings _rabbitMQSettings;
    private readonly ILogger<OrderCancelledConsumer> _logger;
    private IConnection? _connection;
    private IChannel? _channel;
    private const string ServiceName = "notification-service";

    public OrderCancelledConsumer(
        IServiceProvider serviceProvider,
        IOptions<RabbitMQSettings> rabbitMQSettings,
        ILogger<OrderCancelledConsumer> logger)
    {
        _serviceProvider = serviceProvider;
        _rabbitMQSettings = rabbitMQSettings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OrderCancelledConsumer starting...");

        try
        {
            var factory = new ConnectionFactory
            {
                HostName = _rabbitMQSettings.HostName,
                Port = _rabbitMQSettings.Port,
                UserName = _rabbitMQSettings.UserName,
                Password = _rabbitMQSettings.Password,
                VirtualHost = _rabbitMQSettings.VirtualHost
            };

            _connection = await factory.CreateConnectionAsync();
            _channel = await _connection.CreateChannelAsync();


            await _channel.ExchangeDeclareAsync("order.events", ExchangeType.Topic, durable: true);
            await _channel.QueueDeclareAsync(_rabbitMQSettings.OrderCancelledQueue, durable: true, exclusive: false, autoDelete: false);
            await _channel.QueueBindAsync(_rabbitMQSettings.OrderCancelledQueue, "order.events", "order.cancelled");

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (model, ea) =>
            {
                using var activity = ActivitySourceProvider.StartActivity(ServiceName, "ProcessOrderCancelledEvent", ActivityKind.Consumer);

                try
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);

                    _logger.LogInformation("Received order.cancelled event: {Message}", message);

                    var orderEvent = JsonSerializer.Deserialize<OrderCancelledEvent>(message);
                    if (orderEvent != null)
                    {
                        activity?.SetTag("order.id", orderEvent.OrderId);
                        activity?.SetTag("user.id", orderEvent.UserId);

                        await ProcessOrderCancelledEventAsync(orderEvent);

                        await _channel.BasicAckAsync(ea.DeliveryTag, false);
                        activity?.SetOkStatus();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing order.cancelled event");
                    activity?.RecordException(ex);


                    await _channel.BasicNackAsync(ea.DeliveryTag, false, true);
                }
            };

            await _channel.BasicConsumeAsync(_rabbitMQSettings.OrderCancelledQueue, autoAck: false, consumer);

            _logger.LogInformation("OrderCancelledConsumer started and listening to {Queue}", _rabbitMQSettings.OrderCancelledQueue);


            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in OrderCancelledConsumer");
            throw;
        }
    }

    private async Task ProcessOrderCancelledEventAsync(OrderCancelledEvent orderEvent)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var smsService = scope.ServiceProvider.GetRequiredService<ISmsService>();

        try
        {

            var templateData = new
            {
                userName = orderEvent.UserName,
                orderId = orderEvent.OrderId.ToString(),
                cancellationReason = orderEvent.CancellationReason ?? "As requested"
            };

            var emailBody = await emailService.RenderTemplateAsync("OrderCancelled", templateData);


            var emailNotification = new Notification
            {
                UserId = orderEvent.UserId,
                Email = orderEvent.Email,
                NotificationType = "Email",
                TemplateName = "OrderCancelled",
                Subject = $"Order Cancellation - Order #{orderEvent.OrderId}",
                Body = emailBody,
                TemplateData = JsonSerializer.Serialize(templateData),
                Status = "Pending",
                ReferenceId = orderEvent.OrderId.ToString(),
                ReferenceType = "Order"
            };

            dbContext.Notifications.Add(emailNotification);
            await dbContext.SaveChangesAsync();


            var emailSuccess = await emailService.SendEmailAsync(emailNotification);

            if (emailSuccess)
            {
                _logger.LogInformation("Order cancellation email sent to {Email} for order {OrderId}",
                    orderEvent.Email, orderEvent.OrderId);
            }
            else
            {
                _logger.LogWarning("Failed to send order cancellation email to {Email} for order {OrderId}",
                    orderEvent.Email, orderEvent.OrderId);
            }


            if (!string.IsNullOrEmpty(orderEvent.PhoneNumber))
            {
                var smsBody = $"Your order #{orderEvent.OrderId} has been cancelled. Reason: {orderEvent.CancellationReason ?? "As requested"}. Any charges will be refunded. Atlas E-Commerce";

                var smsNotification = new Notification
                {
                    UserId = orderEvent.UserId,
                    PhoneNumber = orderEvent.PhoneNumber,
                    NotificationType = "SMS",
                    TemplateName = "OrderCancelledSMS",
                    Body = smsBody,
                    Status = "Pending",
                    ReferenceId = orderEvent.OrderId.ToString(),
                    ReferenceType = "Order"
                };

                dbContext.Notifications.Add(smsNotification);
                await dbContext.SaveChangesAsync();

                var smsSuccess = await smsService.SendSmsAsync(smsNotification);

                if (smsSuccess)
                {
                    _logger.LogInformation("Order cancellation SMS sent to {PhoneNumber} for order {OrderId}",
                        orderEvent.PhoneNumber, orderEvent.OrderId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing order cancelled event for order {OrderId}", orderEvent.OrderId);
            throw;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel != null)
            await _channel.CloseAsync();
        if (_connection != null)
            await _connection.CloseAsync();
        await base.StopAsync(cancellationToken);
    }
}
