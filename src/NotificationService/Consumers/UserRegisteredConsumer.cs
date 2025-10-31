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




public class UserRegisteredConsumer : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly RabbitMQSettings _rabbitMQSettings;
    private readonly ILogger<UserRegisteredConsumer> _logger;
    private IConnection? _connection;
    private IChannel? _channel;
    private const string ServiceName = "notification-service";

    public UserRegisteredConsumer(
        IServiceProvider serviceProvider,
        IOptions<RabbitMQSettings> rabbitMQSettings,
        ILogger<UserRegisteredConsumer> logger)
    {
        _serviceProvider = serviceProvider;
        _rabbitMQSettings = rabbitMQSettings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("UserRegisteredConsumer starting...");

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
            await _channel.QueueDeclareAsync(_rabbitMQSettings.UserRegisteredQueue, durable: true, exclusive: false, autoDelete: false);
            await _channel.QueueBindAsync(_rabbitMQSettings.UserRegisteredQueue, "user.events", "user.registered");

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (model, ea) =>
            {
                using var activity = ActivitySourceProvider.StartActivity(ServiceName, "ProcessUserRegisteredEvent", ActivityKind.Consumer);

                try
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);

                    _logger.LogInformation("Received user.registered event: {Message}", message);

                    var userEvent = JsonSerializer.Deserialize<UserRegisteredEvent>(message);
                    if (userEvent != null)
                    {
                        activity?.SetTag("user.id", userEvent.UserId);
                        activity?.SetTag("user.email", userEvent.Email);

                        await ProcessUserRegisteredEventAsync(userEvent);

                        await _channel.BasicAckAsync(ea.DeliveryTag, false);
                        activity?.SetOkStatus();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing user.registered event");
                    activity?.RecordException(ex);


                    await _channel.BasicNackAsync(ea.DeliveryTag, false, true);
                }
            };

            await _channel.BasicConsumeAsync(_rabbitMQSettings.UserRegisteredQueue, autoAck: false, consumer);

            _logger.LogInformation("UserRegisteredConsumer started and listening to {Queue}", _rabbitMQSettings.UserRegisteredQueue);


            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in UserRegisteredConsumer");
            throw;
        }
    }

    private async Task ProcessUserRegisteredEventAsync(UserRegisteredEvent userEvent)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        try
        {

            var templateData = new
            {
                userName = userEvent.UserName,
                email = userEvent.Email,
                loginUrl = "http://localhost:5004/Account/Login"
            };

            var emailBody = await emailService.RenderTemplateAsync("WelcomeEmail", templateData);


            var notification = new Notification
            {
                UserId = userEvent.UserId,
                Email = userEvent.Email,
                PhoneNumber = userEvent.PhoneNumber,
                NotificationType = "Email",
                TemplateName = "WelcomeEmail",
                Subject = "Welcome to Atlas E-Commerce!",
                Body = emailBody,
                TemplateData = JsonSerializer.Serialize(templateData),
                Status = "Pending",
                ReferenceId = userEvent.UserId,
                ReferenceType = "User"
            };

            dbContext.Notifications.Add(notification);
            await dbContext.SaveChangesAsync();


            var success = await emailService.SendEmailAsync(notification);

            if (success)
            {
                _logger.LogInformation("Welcome email sent to {Email} for user {UserId}", userEvent.Email, userEvent.UserId);
            }
            else
            {
                _logger.LogWarning("Failed to send welcome email to {Email} for user {UserId}", userEvent.Email, userEvent.UserId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing user registered event for user {UserId}", userEvent.UserId);
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
