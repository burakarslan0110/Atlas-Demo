using NotificationService.Models;

namespace NotificationService.Services;




public interface IEmailService
{





    Task<bool> SendEmailAsync(Notification notification);








    Task<bool> SendEmailAsync(string toEmail, string subject, string body);







    Task<string> RenderTemplateAsync(string templateName, object templateData);
}
