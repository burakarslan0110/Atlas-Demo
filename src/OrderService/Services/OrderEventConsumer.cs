using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using Atlas.Common.Constants;

namespace OrderService.Services;

public class OrderEventConsumer : BackgroundService
{
    private readonly ILogger<OrderEventConsumer> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private IConnection? _connection;
    private IModel? _channel;

    public OrderEventConsumer(
        ILogger<OrderEventConsumer> logger,
        IServiceProvider serviceProvider,
        IConfiguration configuration)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _configuration = configuration;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Order Event Consumer is starting");

        var rabbitMqHost = _configuration["RabbitMQ:HostName"] ?? "localhost";
        var rabbitMqUser = _configuration["RabbitMQ:UserName"] ?? "guest";
        var rabbitMqPass = _configuration["RabbitMQ:Password"] ?? "guest";
        var rabbitMqUrl = $"amqp://{rabbitMqUser}:{rabbitMqPass}@{rabbitMqHost}:5672";

        var factory = new ConnectionFactory { Uri = new Uri(rabbitMqUrl) };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();


        _channel.ExchangeDeclare(
            exchange: AppConstants.Exchanges.OrderEvents,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false);


        _channel.QueueDeclare(
            queue: "order-notifications-queue",
            durable: true,
            exclusive: false,
            autoDelete: false);


        _channel.QueueBind(
            queue: "order-notifications-queue",
            exchange: AppConstants.Exchanges.OrderEvents,
            routingKey: AppConstants.EventTypes.OrderCreated);

        _logger.LogInformation("Order Event Consumer initialized");

        return base.StartAsync(cancellationToken);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        stoppingToken.ThrowIfCancellationRequested();

        var consumer = new EventingBasicConsumer(_channel);
        consumer.Received += async (model, eventArgs) =>
        {
            var body = eventArgs.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);

            _logger.LogInformation("Received order event: {Message}", message);

            try
            {

                await ProcessOrderEventAsync(message);


                _channel?.BasicAck(deliveryTag: eventArgs.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing order event: {Message}", message);


                _channel?.BasicNack(deliveryTag: eventArgs.DeliveryTag, multiple: false, requeue: true);
            }
        };

        _channel?.BasicConsume(
            queue: "order-notifications-queue",
            autoAck: false,
            consumer: consumer);

        return Task.CompletedTask;
    }

    private async Task ProcessOrderEventAsync(string message)
    {
        using var scope = _serviceProvider.CreateScope();

        var eventData = JsonSerializer.Deserialize<OrderEventData>(message);

        if (eventData?.OrderId != null)
        {
            _logger.LogInformation("Processing order notification for OrderId: {OrderId}", eventData.OrderId);







            await Task.CompletedTask;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Order Event Consumer is stopping");

        _channel?.Close();
        _connection?.Close();

        await base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
        base.Dispose();
    }
}

public class OrderEventData
{
    public Guid? OrderId { get; set; }
    public string? Action { get; set; }
    public DateTime Timestamp { get; set; }
}
