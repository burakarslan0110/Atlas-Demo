namespace NotificationService.Models;

public class EmailSettings
{
    public string SmtpHost { get; set; } = "smtp.gmail.com";
    public int SmtpPort { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = "Atlas E-Commerce";
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
