using Microsoft.Extensions.DependencyInjection;

namespace Atlas.EventBus;

public static class EventBusExtensions
{
    public static IServiceCollection AddRabbitMQEventBus(this IServiceCollection services, string connectionString)
    {
        services.AddSingleton<IEventPublisher>(sp => new RabbitMQEventPublisher(connectionString));
        return services;
    }
}
