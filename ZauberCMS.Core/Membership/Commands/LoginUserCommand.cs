using System.ComponentModel.DataAnnotations;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using ZauberCMS.Core.Membership.Models;

namespace ZauberCMS.Core.Membership.Commands;

public class LoginUserCommand : IRequest<AuthenticationResult>
{
    [Required]
    [EmailAddress]
    [Display(Name = "Email")]
    public string? Email { get; set; }

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string? Password { get; set; }

    [Display(Name = "Remember me?")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }

    public List<AuthenticationScheme> ExternalLogins { get; set; } = new();
}