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




public class OrderCreatedConsumer : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly RabbitMQSettings _rabbitMQSettings;
    private readonly ILogger<OrderCreatedConsumer> _logger;
    private IConnection? _connection;
    private IChannel? _channel;
    private const string ServiceName = "notification-service";

    public OrderCreatedConsumer(
        IServiceProvider serviceProvider,
        IOptions<RabbitMQSettings> rabbitMQSettings,
        ILogger<OrderCreatedConsumer> logger)
    {
        _serviceProvider = serviceProvider;
        _rabbitMQSettings = rabbitMQSettings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OrderCreatedConsumer starting...");

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
            await _channel.QueueDeclareAsync(_rabbitMQSettings.OrderCreatedQueue, durable: true, exclusive: false, autoDelete: false);
            await _channel.QueueBindAsync(_rabbitMQSettings.OrderCreatedQueue, "order.events", "order.created");

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (model, ea) =>
            {
                using var activity = ActivitySourceProvider.StartActivity(ServiceName, "ProcessOrderCreatedEvent", ActivityKind.Consumer);

                try
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);

                    _logger.LogInformation("Received order.created event: {Message}", message);

                    var orderEvent = JsonSerializer.Deserialize<OrderCreatedEvent>(message);
                    if (orderEvent != null)
                    {
                        activity?.SetTag("order.id", orderEvent.OrderId);
                        activity?.SetTag("user.id", orderEvent.UserId);

                        await ProcessOrderCreatedEventAsync(orderEvent);

                        await _channel.BasicAckAsync(ea.DeliveryTag, false);
                        activity?.SetOkStatus();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing order.created event");
                    activity?.RecordException(ex);


                    await _channel.BasicNackAsync(ea.DeliveryTag, false, true);
                }
            };

            await _channel.BasicConsumeAsync(_rabbitMQSettings.OrderCreatedQueue, autoAck: false, consumer);

            _logger.LogInformation("OrderCreatedConsumer started and listening to {Queue}", _rabbitMQSettings.OrderCreatedQueue);


            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in OrderCreatedConsumer");
            throw;
        }
    }

    private async Task ProcessOrderCreatedEventAsync(OrderCreatedEvent orderEvent)
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
                totalAmount = orderEvent.TotalAmount.ToString("F2"),
                orderDate = orderEvent.OrderDate.ToString("yyyy-MM-dd HH:mm:ss")
            };

            var emailBody = await emailService.RenderTemplateAsync("OrderConfirmation", templateData);


            var emailNotification = new Notification
            {
                UserId = orderEvent.UserId,
                Email = orderEvent.Email,
                NotificationType = "Email",
                TemplateName = "OrderConfirmation",
                Subject = $"Order Confirmation - Order #{orderEvent.OrderId}",
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
                _logger.LogInformation("Order confirmation email sent to {Email} for order {OrderId}",
                    orderEvent.Email, orderEvent.OrderId);
            }


            var adminPhoneNumber = "+905539283582";
            var smsBody = $"Yeni siparis! Siparis No: #{orderEvent.OrderId}, Tutar: {orderEvent.TotalAmount:F2} TL. Atlas E-Commerce";

            var smsNotification = new Notification
            {
                UserId = orderEvent.UserId,
                PhoneNumber = adminPhoneNumber,
                NotificationType = "SMS",
                TemplateName = "OrderConfirmationSMS",
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
                _logger.LogInformation("Order confirmation SMS sent to admin {PhoneNumber} for order {OrderId}",
                    adminPhoneNumber, orderEvent.OrderId);
            }
            else
            {
                _logger.LogWarning("Failed to send order confirmation SMS to admin for order {OrderId}", orderEvent.OrderId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing order created event for order {OrderId}", orderEvent.OrderId);
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
