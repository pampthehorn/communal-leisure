using System.Collections.Generic;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace website.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string subject, string body, IEnumerable<string>? bccEmails = null);
    }


    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body, IEnumerable<string>? bccEmails = null)
        {
            string? host = _configuration.GetValue<string>("Umbraco:CMS:Global:Smtp:Host");
            int port = _configuration.GetValue<int>("Umbraco:CMS:Global:Smtp:Port");
            string? fromAddress = _configuration.GetValue<string>("Umbraco:CMS:Global:Smtp:From");
            string? userName = _configuration.GetValue<string>("Umbraco:CMS:Global:Smtp:Username");
            string? password = _configuration.GetValue<string>("Umbraco:CMS:Global:Smtp:Password");

            if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(fromAddress))
            {
                _logger.LogError("SMTP not configured — cannot send '{Subject}' to {ToEmail}", subject, toEmail);
                throw new InvalidOperationException("SMTP settings are not configured in appsettings.json.");
            }

            MailAddress fromMailAddress;
            MailAddress toMailAddress;
            try
            {
                fromMailAddress = new MailAddress(fromAddress, "Communal Leisure");
                toMailAddress = new MailAddress(toEmail);
            }
            catch (FormatException ex)
            {
                _logger.LogError(ex, "Invalid email address format — cannot send '{Subject}' to {ToEmail} from {FromAddress}", subject, toEmail, fromAddress);
                throw;
            }

            var bccList = bccEmails?
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .ToList() ?? new List<string>();

            using (var mailMessage = new MailMessage(fromMailAddress, toMailAddress))
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.IsBodyHtml = true;

                foreach (var address in bccList)
                {
                    mailMessage.Bcc.Add(address);
                }

                using (var smtpClient = new SmtpClient(host, port))
                {
                    if (!string.IsNullOrEmpty(userName) && !string.IsNullOrEmpty(password))
                    {
                        smtpClient.Credentials = new NetworkCredential(userName, password);
                        smtpClient.UseDefaultCredentials = false;
                        // smtpClient.EnableSsl = true;
                    }

                    try
                    {
                        await smtpClient.SendMailAsync(mailMessage);
                        _logger.LogInformation(
                            "Sent email '{Subject}' to {ToEmail} (bcc: {BccCount}) via {Host}:{Port}",
                            subject, toEmail, bccList.Count, host, port);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Failed to send email '{Subject}' to {ToEmail} (bcc: {BccCount}) via {Host}:{Port}",
                            subject, toEmail, bccList.Count, host, port);
                        throw;
                    }
                }
            }
        }
    }


}
