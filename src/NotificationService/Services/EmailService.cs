using Atlas.Tracing;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using NotificationService.Data;
using NotificationService.Models;
using System.Diagnostics;
using System.Text.Json;

namespace NotificationService.Services;




public class EmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly NotificationDbContext _dbContext;
    private readonly ILogger<EmailService> _logger;
    private const string ServiceName = "notification-service";

    public EmailService(
        IOptions<EmailSettings> settings,
        NotificationDbContext dbContext,
        ILogger<EmailService> logger)
    {
        _settings = settings.Value;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<bool> SendEmailAsync(Notification notification)
    {
        using var activity = ActivitySourceProvider.StartActivity(ServiceName, "SendEmail", ActivityKind.Internal);
        activity?.SetTag("notification.id", notification.Id);
        activity?.SetTag("notification.template", notification.TemplateName);

        try
        {
            if (string.IsNullOrEmpty(notification.Email))
            {
                _logger.LogWarning("Notification {NotificationId} has no email address", notification.Id);
                return false;
            }

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
            message.To.Add(new MailboxAddress("", notification.Email));
            message.Subject = notification.Subject ?? "Notification from Atlas E-Commerce";

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = notification.Body
            };
            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();


            await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort,
                _settings.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None);


            if (!string.IsNullOrEmpty(_settings.Username) && !string.IsNullOrEmpty(_settings.Password))
            {
                await client.AuthenticateAsync(_settings.Username, _settings.Password);
            }


            await client.SendAsync(message);
            await client.DisconnectAsync(true);


            notification.Status = "Sent";
            notification.SentAt = DateTime.UtcNow;
            notification.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Email sent successfully to {Email} for notification {NotificationId}",
                notification.Email, notification.Id);

            activity?.SetOkStatus();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email for notification {NotificationId}", notification.Id);


            notification.Status = "Failed";
            notification.ErrorMessage = ex.Message;
            notification.RetryCount++;
            notification.UpdatedAt = DateTime.UtcNow;


            if (notification.RetryCount < notification.MaxRetries)
            {
                notification.Status = "Retry";
            }

            await _dbContext.SaveChangesAsync();

            activity?.RecordException(ex);
            return false;
        }
    }

    public async Task<bool> SendEmailAsync(string toEmail, string subject, string body)
    {
        using var activity = ActivitySourceProvider.StartActivity(ServiceName, "SendEmail", ActivityKind.Internal);
        activity?.SetTag("email.to", toEmail);
        activity?.SetTag("email.subject", subject);

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
            message.To.Add(new MailboxAddress("", toEmail));
            message.Subject = subject;

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = body
            };
            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();

            await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort,
                _settings.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None);

            if (!string.IsNullOrEmpty(_settings.Username) && !string.IsNullOrEmpty(_settings.Password))
            {
                await client.AuthenticateAsync(_settings.Username, _settings.Password);
            }

            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Email sent successfully to {Email}", toEmail);
            activity?.SetOkStatus();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}", toEmail);
            activity?.RecordException(ex);
            return false;
        }
    }

    public async Task<string> RenderTemplateAsync(string templateName, object templateData)
    {
        using var activity = ActivitySourceProvider.StartActivity(ServiceName, "RenderTemplate", ActivityKind.Internal);
        activity?.SetTag("template.name", templateName);

        try
        {

            var templateDataJson = JsonSerializer.Serialize(templateData);
            var data = JsonSerializer.Deserialize<Dictionary<string, object>>(templateDataJson);

            string template = templateName switch
            {
                "WelcomeEmail" => await GetWelcomeEmailTemplate(),
                "OrderConfirmation" => await GetOrderConfirmationTemplate(),
                "OrderCancelled" => await GetOrderCancelledTemplate(),
                _ => "<html><body><h1>Notification</h1><p>{{message}}</p></body></html>"
            };


            if (data != null)
            {
                foreach (var kvp in data)
                {
                    template = template.Replace($"{{{{{kvp.Key}}}}}", kvp.Value?.ToString() ?? "");
                }
            }

            activity?.SetOkStatus();
            return template;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to render template {TemplateName}", templateName);
            activity?.RecordException(ex);
            return $"<html><body><h1>Error rendering template: {templateName}</h1></body></html>";
        }
    }

    private Task<string> GetWelcomeEmailTemplate()
    {
        return Task.FromResult(@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body { font-family: Arial, sans-serif; background-color: #f4f4f4; padding: 20px; }
        .container { background-color: white; padding: 30px; border-radius: 10px; max-width: 600px; margin: 0 auto; }
        h1 { color: #333; }
        .button { background-color: #007bff; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px; display: inline-block; margin-top: 20px; }
    </style>
</head>
<body>
    <div class='container'>
        <h1>Welcome to Atlas E-Commerce!</h1>
        <p>Hello {{userName}},</p>
        <p>Thank you for registering with Atlas E-Commerce. We're excited to have you on board!</p>
        <p>Your account has been successfully created with the email: <strong>{{email}}</strong></p>
        <p>You can now start shopping for amazing products.</p>
        <a href='{{loginUrl}}' class='button'>Start Shopping</a>
        <p>Best regards,<br/>The Atlas Team</p>
    </div>
</body>
</html>");
    }

    private Task<string> GetOrderConfirmationTemplate()
    {
        return Task.FromResult(@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body { font-family: Arial, sans-serif; background-color: #f4f4f4; padding: 20px; }
        .container { background-color: white; padding: 30px; border-radius: 10px; max-width: 600px; margin: 0 auto; }
        h1 { color: #28a745; }
        .order-details { background-color: #f8f9fa; padding: 15px; border-radius: 5px; margin: 20px 0; }
    </style>
</head>
<body>
    <div class='container'>
        <h1>Order Confirmed!</h1>
        <p>Hello {{userName}},</p>
        <p>Thank you for your order! We've received your order and it's being processed.</p>
        <div class='order-details'>
            <h3>Order Details:</h3>
            <p><strong>Order ID:</strong> {{orderId}}</p>
            <p><strong>Total Amount:</strong> ${{totalAmount}}</p>
            <p><strong>Order Date:</strong> {{orderDate}}</p>
        </div>
        <p>You will receive another email when your order ships.</p>
        <p>Best regards,<br/>The Atlas Team</p>
    </div>
</body>
</html>");
    }

    private Task<string> GetOrderCancelledTemplate()
    {
        return Task.FromResult(@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body { font-family: Arial, sans-serif; background-color: #f4f4f4; padding: 20px; }
        .container { background-color: white; padding: 30px; border-radius: 10px; max-width: 600px; margin: 0 auto; }
        h1 { color: #dc3545; }
    </style>
</head>
<body>
    <div class='container'>
        <h1>Order Cancelled</h1>
        <p>Hello {{userName}},</p>
        <p>Your order #{{orderId}} has been cancelled as requested.</p>
        <p>If you have any questions, please contact our support team.</p>
        <p>Best regards,<br/>The Atlas Team</p>
    </div>
</body>
</html>");
    }
}
