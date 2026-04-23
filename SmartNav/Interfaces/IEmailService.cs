namespace SmartNav.Interfaces
{
    public interface IEmailService
    {
        Task SendVerificationEmail(string toEmail, string token);
        Task SendForgotPasswordEmail(string toEmail, string temporaryPassword);
        Task SendResetPasswordLinkEmail(string toEmail, string resetUrl);
    }
}
