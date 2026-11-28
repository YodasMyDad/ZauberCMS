using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using ZauberCMS.Components.Account.Pages;

namespace ZauberCMS.Components.Tests.Account;

/// <summary>
/// ZA-306: the reset-password page decodes the code of the emailed link (Base64Url). A link a mail client cut short
/// or re-wrapped threw <c>FormatException</c> from the page, so the visitor got the error page instead of being told
/// the link is not valid (the email confirmation already caught it). The template smoke test opens such a link too.
/// </summary>
public class ResetPasswordLinkTests
{
    [Fact]
    public void ZA306_a_link_s_code_is_decoded()
    {
        var token = "CfDJ8+token/with=signs";
        Assert.Equal(token, ResetPassword.DecodeCode(WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token))));
    }

    [Theory]
    [InlineData("a")] // cut short: one character past a whole group
    [InlineData("abc*def")] // a character Base64Url does not have
    public void ZA306_a_damaged_code_is_not_valid_instead_of_throwing(string code) =>
        Assert.Null(ResetPassword.DecodeCode(code));
}
