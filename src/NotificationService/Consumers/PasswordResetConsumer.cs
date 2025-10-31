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




public class PasswordResetConsumer : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly RabbitMQSettings _rabbitMQSettings;
    private readonly ILogger<PasswordResetConsumer> _logger;
    private IConnection? _connection;
    private IChannel? _channel;
    private const string ServiceName = "notification-service";

    public PasswordResetConsumer(
        IServiceProvider serviceProvider,
        IOptions<RabbitMQSettings> rabbitMQSettings,
        ILogger<PasswordResetConsumer> logger)
    {
        _serviceProvider = serviceProvider;
        _rabbitMQSettings = rabbitMQSettings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PasswordResetConsumer starting...");

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


            await _channel.ExchangeDeclareAsync("user.events", ExchangeType.Topic, durable: true);
            await _channel.QueueDeclareAsync("notification.password.reset", durable: true, exclusive: false, autoDelete: false);
            await _channel.QueueBindAsync("notification.password.reset", "user.events", "password.reset.requested");

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (model, ea) =>
            {
                using var activity = ActivitySourceProvider.StartActivity(ServiceName, "ProcessPasswordResetEvent", ActivityKind.Consumer);

                try
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);

                    _logger.LogInformation("Received password.reset.requested event: {Message}", message);

                    var resetEvent = JsonSerializer.Deserialize<PasswordResetRequestedEvent>(message);
                    if (resetEvent != null)
                    {
                        activity?.SetTag("user.id", resetEvent.UserId);
                        activity?.SetTag("user.email", resetEvent.Email);

                        await ProcessPasswordResetEventAsync(resetEvent);

                        await _channel.BasicAckAsync(ea.DeliveryTag, false);
                        activity?.SetOkStatus();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing password.reset.requested event");
                    activity?.RecordException(ex);


                    await _channel.BasicNackAsync(ea.DeliveryTag, false, true);
                }
            };

            await _channel.BasicConsumeAsync("notification.password.reset", autoAck: false, consumer);

            _logger.LogInformation("PasswordResetConsumer started and listening to notification.password.reset");


            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in PasswordResetConsumer");
            throw;
        }
    }

    private async Task ProcessPasswordResetEventAsync(PasswordResetRequestedEvent resetEvent)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        try
        {

            var templateData = new
            {
                userName = resetEvent.UserName,
                resetUrl = resetEvent.ResetUrl,
                expiryMinutes = 60
            };

            var emailBody = await emailService.RenderTemplateAsync("PasswordReset", templateData);


            var notification = new Notification
            {
                UserId = resetEvent.UserId,
                Email = resetEvent.Email,
                NotificationType = "Email",
                TemplateName = "PasswordReset",
                Subject = "Password Reset Request - Atlas E-Commerce",
                Body = emailBody,
                TemplateData = JsonSerializer.Serialize(templateData),
                Status = "Pending",
                ReferenceId = resetEvent.UserId,
                ReferenceType = "User"
            };

            dbContext.Notifications.Add(notification);
            await dbContext.SaveChangesAsync();


            var success = await emailService.SendEmailAsync(notification);

            if (success)
            {
                _logger.LogInformation("Password reset email sent to {Email} for user {UserId}", resetEvent.Email, resetEvent.UserId);
            }
            else
            {
                _logger.LogWarning("Failed to send password reset email to {Email} for user {UserId}", resetEvent.Email, resetEvent.UserId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing password reset event for user {UserId}", resetEvent.UserId);
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
