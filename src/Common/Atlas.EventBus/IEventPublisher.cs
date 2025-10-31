namespace Atlas.EventBus;

public interface IEventPublisher
{
    void Publish<T>(string exchange, string routingKey, T message) where T : class;
    Task PublishAsync<T>(string exchange, string routingKey, T message) where T : class;
}
