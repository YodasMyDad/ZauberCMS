using System.ComponentModel.DataAnnotations;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Membership.Validation;

namespace ZauberCMS.Core.Membership.Parameters;

public class RegisterUserParameters
{
    [ZauberRequired]
    [ZauberStringLength(150)]
    [Display(Name = "Username")]
    public string Username { get; set; } = string.Empty;

    [ZauberRequired]
    [ZauberEmailAddress]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [ZauberRequired]
    [ZauberStringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 8)]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Remember me?")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }

    public bool AutoLogin { get; set; } = true;
    
    public string? Name { get; set; }
}