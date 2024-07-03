using System.Security.Claims;

namespace ZauberCMS.Core.Extensions;

/// <summary>
/// Claims principle extensions
/// </summary>
/// <remarks>
/// https://www.jerriepelser.com/blog/useful-claimsprincipal-extension-methods/
/// </remarks>
public static class ClaimsPrincipalExtensions
{
    public static string? GetUserEmail(this ClaimsPrincipal principal)
    {
        return principal.FindFirstValue(ClaimTypes.Email);
    }

    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        return Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException());
    }

    public static string? GetUserName(this ClaimsPrincipal principal)
    {
        return principal.FindFirstValue(ClaimTypes.Name);
    }

    public static string? GetProfileImage(this ClaimsPrincipal principal)
    {
        return principal.FindFirstValue(Constants.Claims.ProfileImage);
    }
    
    public static string? GetMd5(this ClaimsPrincipal principal)
    {
        return principal.FindFirstValue(Constants.Claims.Md5Hash);
    }

    public static bool HasProfileImage(this ClaimsPrincipal principal)
    {
        return principal.HasClaim(c => c.Type == Constants.Claims.ProfileImage);
    }
}