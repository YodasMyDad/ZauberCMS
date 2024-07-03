using System.ComponentModel.DataAnnotations;
using MediatR;
using ZauberCMS.Core.Membership.Models;

namespace ZauberCMS.Core.Membership.Commands;

public class LoginUserCommand : IRequest<AuthenticationResult>
{
    [Required]
    [EmailAddress]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Remember me?")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}