using Microsoft.AspNetCore.Http;
using ZauberCMS.Components.Controllers;

namespace ZauberCMS.Components.Tests.Shared;

/// <summary>
/// ZA-030: the sign-out address submits itself only for a navigation from this site; a link from another site gets
/// a button, so other sites cannot sign visitors out.
/// </summary>
public class LogoutTests
{
    [Theory]
    [InlineData("same-origin", null, true)]
    [InlineData("none", null, true)]
    [InlineData("cross-site", null, false)]
    [InlineData("same-site", null, false)]
    [InlineData(null, null, false)]
    [InlineData(null, "https://site.test/admin", true)]
    [InlineData(null, "https://evil.example/page", false)]
    public void A_sign_out_signs_out_at_once_only_when_it_came_from_this_site(string? fetchSite, string? referer,
        bool expected)
    {
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("site.test");
        if (fetchSite != null)
        {
            context.Request.Headers["Sec-Fetch-Site"] = fetchSite;
        }

        if (referer != null)
        {
            context.Request.Headers.Referer = referer;
        }

        Assert.Equal(expected, AuthController.IsFromThisSite(context.Request));
    }
}
