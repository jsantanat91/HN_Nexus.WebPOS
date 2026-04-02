namespace HN_Nexus.WebPOS.Services;

public interface IAlertEmailService
{
    Task<bool> SendAlertAsync(string subject, string body);
    Task<bool> SendToAsync(string toEmail, string subject, string body, bool isHtml = false);
}
