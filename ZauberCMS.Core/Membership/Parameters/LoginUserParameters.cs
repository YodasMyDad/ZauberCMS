using System.ComponentModel.DataAnnotations;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Membership.Validation;

namespace ZauberCMS.Core.Membership.Parameters;

public class LoginUserParameters
{
    [ZauberRequired]
    [ZauberEmailAddress]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [ZauberRequired]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Remember me?")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}