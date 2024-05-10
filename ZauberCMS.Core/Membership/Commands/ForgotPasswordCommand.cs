using System.ComponentModel.DataAnnotations;
using MediatR;
using ZauberCMS.Core.Membership.Models;

namespace ZauberCMS.Core.Membership.Commands;

public class ForgotPasswordCommand : IRequest<AuthenticationResult>
{
    [Required]
    [EmailAddress]
    public string? Email { get; set; }
}