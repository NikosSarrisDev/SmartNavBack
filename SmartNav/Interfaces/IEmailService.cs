namespace SmartNav.Interfaces
{
    public interface IEmailService
    {
        Task SendVerificationEmail(string toEmail, string token);
    }
}
