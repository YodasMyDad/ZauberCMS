using ZauberCMS.Core.Membership.Models;

namespace ZauberCMS.Core.Email.Parameters;

public class SendEmailConfirmationParameters
{
    public User? User { get; set; }
    public string? NewEmailAddress { get; set; }
    public string? ReturnUrl { get; set; } = "/";
}