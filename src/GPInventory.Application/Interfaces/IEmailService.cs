namespace GPInventory.Application.Interfaces;

public interface IEmailService
{
    Task SendPasswordResetEmailAsync(string toEmail, string toName, string resetLink);
    Task SendWelcomeEmailAsync(string toEmail, string toName);
    Task SendEmailVerificationAsync(string toEmail, string toName, string verifyLink);
}
