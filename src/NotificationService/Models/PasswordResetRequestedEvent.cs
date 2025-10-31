namespace NotificationService.Models;

public class PasswordResetRequestedEvent
{
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string ResetToken { get; set; } = string.Empty;
    public string ResetUrl { get; set; } = string.Empty;
}
