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
            var verificationUrl = $"https://localhost:44396/api/User/VerifyEmail?token={token}";

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

            await SendEmailAsync(toEmail, "Activate Your Account", builder.ToMessageBody());
        }

        public async Task SendForgotPasswordEmail(string toEmail, string temporaryPassword)
        {
            var builder = new BodyBuilder
            {
                HtmlBody = $@"
                <div style='font-family: Arial, sans-serif; max-width: 500px; padding: 20px; border: 1px solid #eee;'>
                    <h2 style='color: #2c3e50;'>SmartNav Password Recovery</h2>
                    <p>Your temporary password is:</p>
                    <div style='margin: 20px 0; padding: 12px; background: #f4f6f8; border-radius: 4px; font-size: 18px; font-weight: 700; letter-spacing: 1px;'>
                        {temporaryPassword}
                    </div>
                    <p>Please log in with this password and change it as soon as possible.</p>
                    <p style='font-size: 12px; color: #95a5a6;'>If you didn't request this, ignore this email and contact support.</p>
                </div>"
            };

            await SendEmailAsync(toEmail, "SmartNav Password Recovery", builder.ToMessageBody());
        }

        public async Task SendResetPasswordLinkEmail(string toEmail, string resetUrl)
        {
            var builder = new BodyBuilder
            {
                HtmlBody = $@"
                <div style='font-family: Arial, sans-serif; max-width: 500px; padding: 20px; border: 1px solid #eee;'>
                    <h2 style='color: #2c3e50;'>SmartNav Password Reset</h2>
                    <p>Click the button below to set a new password:</p>
                    <div style='margin: 30px 0; text-align: center;'>
                        <a href='{resetUrl}'
                           style='background: #3498db; color: white; padding: 12px 24px; text-decoration: none; border-radius: 4px; display: inline-block;'>
                           Reset Password
                        </a>
                    </div>
                    <p style='font-size: 12px; color: #95a5a6;'>If you did not request a password reset, ignore this email.</p>
                </div>"
            };

            await SendEmailAsync(toEmail, "SmartNav Password Reset Link", builder.ToMessageBody());
        }

        private async Task SendEmailAsync(string toEmail, string subject, MimeEntity body)
        {
            var settings = _config.GetSection("EmailSettings");

            var email = new MimeMessage();
            email.From.Add(new MailboxAddress(settings["DisplayName"], settings["Email"]));
            email.To.Add(MailboxAddress.Parse(toEmail));
            email.Subject = subject;
            email.Body = body;

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
