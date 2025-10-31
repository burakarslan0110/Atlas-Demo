using NotificationService.Models;

namespace NotificationService.Services;




public interface ISmsService
{





    Task<bool> SendSmsAsync(Notification notification);







    Task<bool> SendSmsAsync(string toPhoneNumber, string message);
}
