using Microsoft.AspNetCore.Identity;
using ZauberCMS.Core.Membership.Models;

namespace ZauberCMS.Core.Membership.Parameters;

public class ExternalLoginParameters
{
    public string? ProviderDisplayName { get; set; }
    public ExternalLoginInfo? ExternalLoginInfo { get; set; }
    public string? ReturnUrl { get; set; }
}