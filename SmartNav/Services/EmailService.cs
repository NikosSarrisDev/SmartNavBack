using MimeKit;
using SmtpClient = MailKit.Net.Smtp.SmtpClient;
using SmartNav.Interfaces;
using MailKit.Security;

namespace SmartNav.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendVerificationEmail(string toEmail, string token)
        {
            var settings = _config.GetSection("EmailSettings");

            var verificationUrl = $"https://localhost:44396/api/User/VerifyEmail?token={token}";

            var email = new MimeMessage();
            email.From.Add(new MailboxAddress(settings["DisplayName"], settings["Email"]));
            email.To.Add(MailboxAddress.Parse(toEmail));
            email.Subject = "Activate Your Account";

            var builder = new BodyBuilder
            {
                HtmlBody = $@"
                <div style='font-family: Arial, sans-serif; max-width: 500px; padding: 20px; border: 1px solid #eee;'>
                    <h2 style='color: #2c3e50;'>Welcome to Smart Nav!</h2>
                    <p>Thank you for joining. Please click the button below to verify your email address:</p>
                    <div style='margin: 30px 0; text-align: center;'>
                        <a href='{verificationUrl}' 
                           style='background: #3498db; color: white; padding: 12px 24px; text-decoration: none; border-radius: 4px; display: inline-block;'>
                           Verify Email
                        </a>
                    </div>
                    <p style='font-size: 12px; color: #95a5a6;'>If you didn't request this, just ignore this email.</p>
                </div>"
            };

            email.Body = builder.ToMessageBody();

            using var smtp = new SmtpClient();
            try
            {
                await smtp.ConnectAsync(settings["Host"], int.Parse(settings["Port"]), SecureSocketOptions.StartTls);

                await smtp.AuthenticateAsync(settings["Email"], settings["Password"]);

                await smtp.SendAsync(email);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SMTP Error: {ex.Message}");
                throw;
            }
            finally
            {
                await smtp.DisconnectAsync(true);
            }
        }
    }
}
