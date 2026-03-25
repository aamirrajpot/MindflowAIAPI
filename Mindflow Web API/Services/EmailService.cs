using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Mindflow_Web_API.Services
{
    public interface IEmailService
    {
        Task<bool> SendEmailAsync(string toEmail, string subject, string body, bool isBodyHtml = false);
    }

    /// <summary>
    /// Email provider:
    /// - If configured: Mailjet SMTP (Email:Mailjet:*). Sends via in-v3.mailjet.com:587.
    /// - Otherwise: falls back to your existing SMTP config (Email:SmtpServer / SenderEmail / SenderPassword).
    /// </summary>
    public class EmailService : IEmailService
    {
        private const string MailjetHost = "in-v3.mailjet.com";
        private const int MailjetPort = 587;

        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<bool> SendEmailAsync(string toEmail, string subject, string body, bool isBodyHtml = false)
        {
            if (string.IsNullOrWhiteSpace(toEmail))
                return false;

            // Prefer Mailjet when configured.
            var mailjetApiKey = _configuration["Email:Mailjet:ApiKey"];
            var mailjetSecretKey = _configuration["Email:Mailjet:SecretKey"];

            if (!string.IsNullOrWhiteSpace(mailjetApiKey) && !string.IsNullOrWhiteSpace(mailjetSecretKey))
            {
                return await SendViaMailjetSmtpAsync(
                    toEmail: toEmail.Trim(),
                    subject: subject,
                    body: body,
                    isBodyHtml: isBodyHtml,
                    apiKey: mailjetApiKey.Trim(),
                    secretKey: mailjetSecretKey.Trim());
            }

            // Fallback to existing SMTP settings.
            var emailConfig = _configuration.GetSection("Email");
            var smtpServer = emailConfig["SmtpServer"];
            var portStr = emailConfig["Port"];
            var senderEmail = emailConfig["SenderEmail"];
            var senderPassword = emailConfig["SenderPassword"];
            var senderName = emailConfig["SenderName"] ?? "Mindflow AI";

            if (string.IsNullOrWhiteSpace(smtpServer) ||
                string.IsNullOrWhiteSpace(portStr) ||
                string.IsNullOrWhiteSpace(senderEmail) ||
                string.IsNullOrWhiteSpace(senderPassword))
            {
                _logger.LogWarning("Email SMTP provider not configured. Set Email:Mailjet:* or Email:SmtpServer/Port/SenderEmail/SenderPassword.");
                return false;
            }

            if (!int.TryParse(portStr, out var port))
            {
                _logger.LogWarning("Email SMTP invalid port: {Port}", portStr);
                return false;
            }

            return await SendViaSmtpCoreAsync(
                toEmail: toEmail.Trim(),
                subject: subject,
                body: body,
                isBodyHtml: isBodyHtml,
                smtpServer: smtpServer,
                port: port,
                smtpUsername: senderEmail,
                smtpPassword: senderPassword,
                fromEmail: senderEmail,
                fromName: senderName);
        }

        private async Task<bool> SendViaMailjetSmtpAsync(
            string toEmail,
            string subject,
            string body,
            bool isBodyHtml,
            string apiKey,
            string secretKey)
        {
            // Mailjet SMTP login:
            // - username = API key
            // - password = secret key
            var senderEmail = _configuration["Email:Mailjet:SenderEmail"] ?? _configuration["Email:SenderEmail"];
            var senderName = _configuration["Email:Mailjet:SenderName"] ?? _configuration["Email:SenderName"] ?? "Mindflow AI";

            if (string.IsNullOrWhiteSpace(senderEmail))
            {
                _logger.LogWarning("Mailjet configured but Email:Mailjet:SenderEmail missing (or Email:SenderEmail missing).");
                return false;
            }

            return await SendViaSmtpCoreAsync(
                toEmail: toEmail,
                subject: subject,
                body: body,
                isBodyHtml: isBodyHtml,
                smtpServer: MailjetHost,
                port: MailjetPort,
                // Mailjet SMTP credentials:
                // username = API key, password = secret key
                smtpUsername: apiKey.Trim(),
                smtpPassword: secretKey,
                fromEmail: senderEmail.Trim(),
                fromName: senderName);
        }

        private async Task<bool> SendViaSmtpCoreAsync(
            string toEmail,
            string subject,
            string body,
            bool isBodyHtml,
            string smtpServer,
            int port,
            string smtpUsername,
            string smtpPassword,
            string fromEmail,
            string fromName)
        {
            using var smtpClient = new SmtpClient(smtpServer)
            {
                Port = port,
                Credentials = new NetworkCredential(smtpUsername, smtpPassword),
                EnableSsl = true
            };

            using var mailMessage = new MailMessage
            {
                From = new MailAddress(fromEmail, fromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = isBodyHtml
            };

            mailMessage.To.Add(toEmail);

            try
            {
                await smtpClient.SendMailAsync(mailMessage);
                _logger.LogInformation("Email sent to {To} via {SmtpServer}", toEmail, smtpServer);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Email send failed to {To} via {SmtpServer}", toEmail, smtpServer);
                return false;
            }
        }
    }
}