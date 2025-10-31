namespace NotificationService.Models;




public class OrderCancelledEvent
{
    public string OrderId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? CancellationReason { get; set; }
    public DateTime CancelledAt { get; set; }
}
