using Atlas.Common.Constants;
using ProductService.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace ProductService.Consumers;

public class StockManagementConsumer : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<StockManagementConsumer> _logger;
    private IConnection? _connection;
    private IModel? _channel;

    public StockManagementConsumer(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<StockManagementConsumer> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stock Management Consumer is starting");

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
            queue: "product-stock-management",
            durable: true,
            exclusive: false,
            autoDelete: false);


        _channel.QueueBind(
            queue: "product-stock-management",
            exchange: AppConstants.Exchanges.OrderEvents,
            routingKey: AppConstants.EventTypes.OrderCreated);

        _logger.LogInformation("Stock Management Consumer initialized");

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

            _logger.LogInformation("Received order.created event");

            try
            {
                var orderEvent = JsonSerializer.Deserialize<OrderCreatedEvent>(message);

                if (orderEvent != null)
                {
                    _logger.LogInformation("Processing order.created event for OrderId: {OrderId}", orderEvent.OrderId);

                    using var scope = _serviceProvider.CreateScope();
                    var productService = scope.ServiceProvider.GetRequiredService<IProductService>();

                    foreach (var item in orderEvent.Items)
                    {
                        var success = await productService.DecreaseStockAsync(item.ProductId, item.Quantity);

                        if (success)
                        {
                            _logger.LogInformation("Stock decreased for ProductId: {ProductId}, Quantity: {Quantity}",
                                item.ProductId, item.Quantity);
                        }
                        else
                        {
                            _logger.LogWarning("Failed to decrease stock for ProductId: {ProductId}, Quantity: {Quantity}",
                                item.ProductId, item.Quantity);
                        }
                    }

                    _logger.LogInformation("Stock management completed for OrderId: {OrderId}", orderEvent.OrderId);
                }


                _channel?.BasicAck(deliveryTag: eventArgs.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing order.created event");


                _channel?.BasicNack(deliveryTag: eventArgs.DeliveryTag, multiple: false, requeue: true);
            }
        };

        _channel?.BasicConsume(
            queue: "product-stock-management",
            autoAck: false,
            consumer: consumer);

        return Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stock Management Consumer is stopping");

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


public class OrderCreatedEvent
{
    public Guid OrderId { get; set; }
    public Guid UserId { get; set; }
    public decimal Total { get; set; }
    public int ItemCount { get; set; }
    public List<OrderItemEvent> Items { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}

public class OrderItemEvent
{
    public string ProductId { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}
