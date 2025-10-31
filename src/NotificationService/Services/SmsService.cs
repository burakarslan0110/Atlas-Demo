using Atlas.Tracing;
using Microsoft.Extensions.Options;
using NotificationService.Data;
using NotificationService.Models;
using System.Diagnostics;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace NotificationService.Services;




public class SmsService : ISmsService
{
    private readonly SmsSettings _settings;
    private readonly NotificationDbContext _dbContext;
    private readonly ILogger<SmsService> _logger;
    private const string ServiceName = "notification-service";

    public SmsService(
        IOptions<SmsSettings> settings,
        NotificationDbContext dbContext,
        ILogger<SmsService> logger)
    {
        _settings = settings.Value;
        _dbContext = dbContext;
        _logger = logger;


        if (!string.IsNullOrEmpty(_settings.AccountSid) && !string.IsNullOrEmpty(_settings.AuthToken))
        {
            TwilioClient.Init(_settings.AccountSid, _settings.AuthToken);
        }
    }

    public async Task<bool> SendSmsAsync(Notification notification)
    {
        using var activity = ActivitySourceProvider.StartActivity(ServiceName, "SendSms", ActivityKind.Internal);
        activity?.SetTag("notification.id", notification.Id);
        activity?.SetTag("notification.template", notification.TemplateName);

        try
        {
            if (!_settings.Enabled)
            {
                _logger.LogWarning("SMS service is disabled in configuration");
                notification.Status = "Skipped";
                notification.ErrorMessage = "SMS service is disabled";
                notification.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();
                return false;
            }

            if (string.IsNullOrEmpty(notification.PhoneNumber))
            {
                _logger.LogWarning("Notification {NotificationId} has no phone number", notification.Id);
                notification.Status = "Failed";
                notification.ErrorMessage = "No phone number provided";
                notification.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();
                return false;
            }


            var messageBody = StripHtmlTags(notification.Body);


            messageBody = TransliterateTurkish(messageBody);


            if (messageBody.Length > 160)
            {
                messageBody = messageBody.Substring(0, 157) + "...";
            }

            var message = await MessageResource.CreateAsync(
                to: new PhoneNumber(notification.PhoneNumber),
                from: new PhoneNumber(_settings.FromPhoneNumber),
                body: messageBody,
                smartEncoded: true
            );

            if (message.Status == MessageResource.StatusEnum.Failed ||
                message.Status == MessageResource.StatusEnum.Undelivered)
            {
                throw new Exception($"Twilio SMS failed with status: {message.Status}");
            }


            notification.Status = "Sent";
            notification.SentAt = DateTime.UtcNow;
            notification.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("SMS sent successfully to {PhoneNumber} for notification {NotificationId}. Twilio SID: {MessageSid}",
                notification.PhoneNumber, notification.Id, message.Sid);

            activity?.SetOkStatus();
            activity?.SetTag("twilio.message_sid", message.Sid);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SMS for notification {NotificationId}", notification.Id);


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

    public async Task<bool> SendSmsAsync(string toPhoneNumber, string message)
    {
        using var activity = ActivitySourceProvider.StartActivity(ServiceName, "SendSms", ActivityKind.Internal);
        activity?.SetTag("sms.to", toPhoneNumber);

        try
        {
            if (!_settings.Enabled)
            {
                _logger.LogWarning("SMS service is disabled in configuration");
                return false;
            }


            var messageBody = TransliterateTurkish(message);


            if (messageBody.Length > 160)
            {
                messageBody = messageBody.Substring(0, 157) + "...";
            }

            var twilioMessage = await MessageResource.CreateAsync(
                to: new PhoneNumber(toPhoneNumber),
                from: new PhoneNumber(_settings.FromPhoneNumber),
                body: messageBody,
                smartEncoded: true
            );

            if (twilioMessage.Status == MessageResource.StatusEnum.Failed ||
                twilioMessage.Status == MessageResource.StatusEnum.Undelivered)
            {
                throw new Exception($"Twilio SMS failed with status: {twilioMessage.Status}");
            }

            _logger.LogInformation("SMS sent successfully to {PhoneNumber}. Twilio SID: {MessageSid}",
                toPhoneNumber, twilioMessage.Sid);

            activity?.SetOkStatus();
            activity?.SetTag("twilio.message_sid", twilioMessage.Sid);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SMS to {PhoneNumber}", toPhoneNumber);
            activity?.RecordException(ex);
            return false;
        }
    }

    private string StripHtmlTags(string html)
    {
        if (string.IsNullOrEmpty(html))
            return string.Empty;


        var result = System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", string.Empty);


        result = System.Net.WebUtility.HtmlDecode(result);


        result = System.Text.RegularExpressions.Regex.Replace(result, @"\s+", " ");

        return result.Trim();
    }

    private string TransliterateTurkish(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;


        var result = text;
        result = result.Replace('ç', 'c').Replace('Ç', 'C');
        result = result.Replace('ğ', 'g').Replace('Ğ', 'G');
        result = result.Replace('ı', 'i').Replace('İ', 'I');
        result = result.Replace('ö', 'o').Replace('Ö', 'O');
        result = result.Replace('ş', 's').Replace('Ş', 'S');
        result = result.Replace('ü', 'u').Replace('Ü', 'U');

        return result;
    }
}
