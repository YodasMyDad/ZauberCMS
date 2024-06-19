using MediatR;
using ZauberCMS.Core.Membership.Models;

namespace ZauberCMS.Core.Email.Commands;

public class SendEmailConfirmationCommand : IRequest
{
    public User? User { get; set; }
    public string? NewEmailAddress { get; set; }
    public string? ReturnUrl { get; set; } = "/";
}