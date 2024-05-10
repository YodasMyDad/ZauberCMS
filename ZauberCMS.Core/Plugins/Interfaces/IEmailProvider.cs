namespace ZauberCMS.Core.Plugins.Interfaces;

public interface IEmailProvider
{
    Task SendEmailWithTemplateAsync(string toEmail, string subject, List<string> paragraphs);
    Task SendEmailAsync(string toEmail, string subject, string message);
}