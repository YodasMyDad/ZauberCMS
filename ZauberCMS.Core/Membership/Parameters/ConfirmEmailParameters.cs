using ZauberCMS.Core.Membership.Models;

namespace ZauberCMS.Core.Membership.Parameters;

public class ConfirmEmailParameters
{
    public string? UserId { get; set; }

    /// <summary>The code exactly as it appears in the emailed link (Base64Url-encoded token).</summary>
    public string? Code { get; set; }

    /// <summary>True when the link confirms a change of email address; <see cref="NewEmailAddress"/> is then required.</summary>
    public bool IsEmailUpdate { get; set; }

    /// <summary>The new address from the emailed link. The code is only valid for this address.</summary>
    public string? NewEmailAddress { get; set; }

    public string? ReturnUrl { get; set; }
}