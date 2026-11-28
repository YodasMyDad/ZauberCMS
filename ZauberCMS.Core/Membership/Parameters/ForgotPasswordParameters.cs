using System.ComponentModel.DataAnnotations;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Membership.Validation;

namespace ZauberCMS.Core.Membership.Parameters;

public class ForgotPasswordParameters
{
    [ZauberRequired]
    [ZauberEmailAddress]
    public string? Email { get; set; }
    public string? ReturnUrl { get; set; }
}