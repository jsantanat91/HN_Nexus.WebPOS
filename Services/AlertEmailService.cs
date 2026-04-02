using System.Net;
using System.Net.Mail;
using HN_Nexus.WebPOS.Data;
using Microsoft.EntityFrameworkCore;

namespace HN_Nexus.WebPOS.Services;

public class AlertEmailService(AppDbContext db) : IAlertEmailService
{
    public async Task<bool> SendAlertAsync(string subject, string body)
    {
        var cfg = await db.AppConfigs.FirstOrDefaultAsync();
        if (cfg is null || string.IsNullOrWhiteSpace(cfg.SmtpHost) || string.IsNullOrWhiteSpace(cfg.AlertFromEmail) || string.IsNullOrWhiteSpace(cfg.AlertToEmails))
        {
            return false;
        }

        var recipients = cfg.AlertToEmails
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
        if (recipients.Count == 0)
        {
            return false;
        }

        using var client = new SmtpClient(cfg.SmtpHost, cfg.SmtpPort <= 0 ? 587 : cfg.SmtpPort)
        {
            EnableSsl = cfg.SmtpUseSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(cfg.SmtpUser ?? string.Empty, cfg.SmtpPassword ?? string.Empty)
        };

        using var msg = new MailMessage
        {
            From = new MailAddress(cfg.AlertFromEmail),
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };

        foreach (var to in recipients)
        {
            msg.To.Add(to);
        }

        await client.SendMailAsync(msg);
        return true;
    }

    public async Task<bool> SendToAsync(string toEmail, string subject, string body, bool isHtml = false)
    {
        var cfg = await db.AppConfigs.FirstOrDefaultAsync();
        if (cfg is null || string.IsNullOrWhiteSpace(cfg.SmtpHost) || string.IsNullOrWhiteSpace(cfg.AlertFromEmail) || string.IsNullOrWhiteSpace(toEmail))
        {
            return false;
        }

        using var client = new SmtpClient(cfg.SmtpHost, cfg.SmtpPort <= 0 ? 587 : cfg.SmtpPort)
        {
            EnableSsl = cfg.SmtpUseSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(cfg.SmtpUser ?? string.Empty, cfg.SmtpPassword ?? string.Empty)
        };

        using var msg = new MailMessage
        {
            From = new MailAddress(cfg.AlertFromEmail),
            Subject = subject,
            Body = body,
            IsBodyHtml = isHtml
        };

        msg.To.Add(toEmail.Trim());
        await client.SendMailAsync(msg);
        return true;
    }
}
