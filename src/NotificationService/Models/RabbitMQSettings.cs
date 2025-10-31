namespace NotificationService.Models;




public class RabbitMQSettings
{
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";


    public string UserRegisteredQueue { get; set; } = "notification.user.registered";
    public string OrderCreatedQueue { get; set; } = "notification.order.created";
    public string OrderCancelledQueue { get; set; } = "notification.order.cancelled";
}
