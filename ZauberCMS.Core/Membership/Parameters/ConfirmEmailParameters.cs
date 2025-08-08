using ZauberCMS.Core.Membership.Models;

namespace ZauberCMS.Core.Membership.Parameters;

public class ConfirmEmailParameters
{
    public string? UserId { get; set; }
    public string? Code { get; set; }
    public bool IsEmailUpdate { get; set; }
    public string? ReturnUrl { get; set; }
}