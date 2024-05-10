using System.ComponentModel.DataAnnotations;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using ZauberCMS.Core.Membership.Models;

namespace ZauberCMS.Core.Membership.Commands;

public class RegisterUserCommand : IRequest<AuthenticationResult>
{
    [Required]
    [StringLength(150)]
    [Display(Name = "Username")]
    public string? Username { get; set; }

    [Required]
    [EmailAddress]
    [Display(Name = "Email")]
    public string? Email { get; set; }

    [Required]
    [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 8)]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string? Password { get; set; }

    [Display(Name = "Remember me?")]
    public bool RememberMe { get; set; }

    public List<AuthenticationScheme> ExternalLogins { get; set; } = new();

    public string? ReturnUrl { get; set; }

    public bool AutoLogin { get; set; } = true;
}