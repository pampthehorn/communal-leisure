using System.Collections.Generic;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace website.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string subject, string body, IEnumerable<string> bccEmails = null);
    }


    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body, IEnumerable<string> bccEmails = null)
        {
            string host = _configuration.GetValue<string>("Umbraco:CMS:Global:Smtp:Host");
            int port = _configuration.GetValue<int>("Umbraco:CMS:Global:Smtp:Port");
            string fromAddress = _configuration.GetValue<string>("Umbraco:CMS:Global:Smtp:From");
            string userName = _configuration.GetValue<string>("Umbraco:CMS:Global:Smtp:Username");
            string password = _configuration.GetValue<string>("Umbraco:CMS:Global:Smtp:Password");

            if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(fromAddress))
            {
                throw new InvalidOperationException("SMTP settings are not configured in appsettings.json.");
            }

            var fromMailAddress = new MailAddress(fromAddress, "Communal Leisure");
            var toMailAddress = new MailAddress(toEmail);

            using (var mailMessage = new MailMessage(fromMailAddress, toMailAddress))
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.IsBodyHtml = true;

                if (bccEmails != null)
                {
                    foreach (var address in bccEmails)
                    {
                        if (!string.IsNullOrWhiteSpace(address))
                        {
                            mailMessage.Bcc.Add(address);
                        }
                    }
                }

                using (var smtpClient = new SmtpClient(host, port))
                {
                    if (!string.IsNullOrEmpty(userName) && !string.IsNullOrEmpty(password))
                    {
                        smtpClient.Credentials = new NetworkCredential(userName, password);
                        smtpClient.UseDefaultCredentials = false;
                        // smtpClient.EnableSsl = true; 
                    }

                    await smtpClient.SendMailAsync(mailMessage);
                }
            }
        }
    }


}