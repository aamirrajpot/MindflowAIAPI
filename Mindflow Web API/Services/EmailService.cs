using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace Mindflow_Web_API.Services
{
    public interface IEmailService
    {
        Task<bool> SendEmailAsync(string toEmail, string subject, string body, bool isBodyHtml = false);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<bool> SendEmailAsync(string toEmail, string subject, string body, bool isBodyHtml = false)
        {
            var emailConfig = _configuration.GetSection("Email");
            var smtpClient = new SmtpClient(emailConfig["SmtpServer"])
            {
                Port = int.Parse(emailConfig["Port"]!),
                Credentials = new NetworkCredential(emailConfig["SenderEmail"], emailConfig["SenderPassword"]),
                EnableSsl = true
            };
            var mailMessage = new MailMessage
            {
                From = new MailAddress(emailConfig["SenderEmail"], emailConfig["SenderName"]),
                Subject = subject,
                Body = body,
                IsBodyHtml = isBodyHtml
            };
            mailMessage.To.Add(toEmail);
            try
            {
                await smtpClient.SendMailAsync(mailMessage);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
} 