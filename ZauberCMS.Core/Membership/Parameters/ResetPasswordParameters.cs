using System.ComponentModel.DataAnnotations;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Membership.Validation;

namespace ZauberCMS.Core.Membership.Parameters;

public class ResetPasswordParameters
{
    [ZauberRequired]
    [ZauberEmailAddress]
    public string? Email { get; set; }

    [ZauberRequired]
    [ZauberStringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 8)]
    [DataType(DataType.Password)]
    public string? Password { get; set; }

    [DataType(DataType.Password)]
    [Display(Name = "Confirm password")]
    [ZauberComparePassword("Password", ErrorMessage = "The password and confirmation password do not match.")]
    public string? ConfirmPassword { get; set; }

    [ZauberRequired]
    public string? Code { get; set; }
    public string? ReturnUrl { get; set; }
}