using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Seo;
using ZauberCMS.Core.Tests.Infrastructure;

namespace ZauberCMS.Core.Tests.Seo;

/// <summary>
/// The SEO checker fetches addresses an admin typed (and links found on those pages). It must not reach addresses
/// only the server can see (ZA-019). Emailed links must use the configured site address, not the Host header of
/// the request that asked for the email (ZA-003).
/// </summary>
public class OutboundRequestTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.1.2.3")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.254")]
    [InlineData("192.168.1.10")]
    [InlineData("169.254.169.254")] // cloud metadata
    [InlineData("100.64.0.1")]
    [InlineData("0.0.0.0")]
    [InlineData("224.0.0.1")]
    [InlineData("::1")]
    [InlineData("fe80::1")]
    [InlineData("fd00::1")]
    [InlineData("::ffff:10.0.0.1")] // IPv4 address written as IPv6
    [InlineData("64:ff9b::a00:1")]
    public void Addresses_only_the_server_can_reach_are_not_public(string address)
    {
        Assert.False(SeoHttp.IsPublicAddress(IPAddress.Parse(address)));
    }

    [Theory]
    [InlineData("93.184.216.34")]
    [InlineData("172.32.0.1")]
    [InlineData("8.8.8.8")]
    [InlineData("2606:4700::1111")]
    public void Internet_addresses_are_public(string address)
    {
        Assert.True(SeoHttp.IsPublicAddress(IPAddress.Parse(address)));
    }

    [Theory]
    [InlineData("http://127.0.0.1:9/")]
    [InlineData("http://localhost:9/")]
    [InlineData("http://169.254.169.254/latest/meta-data/")]
    [InlineData("http://[::1]:9/")]
    public async Task The_SEO_client_refuses_to_connect_to_a_private_address(string url)
    {
        await using var site = await TestSite.CreateAsync();
        var client = site.Services.GetRequiredService<IHttpClientFactory>().CreateClient(SeoHttp.ClientName);

        var error = await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.GetAsync(url, TestContext.Current.CancellationToken));

        Assert.Contains("not a public address", error.ToString());
    }

    [Theory]
    [InlineData("https://www.example.com/page", true)]
    [InlineData("http://example.com", true)]
    [InlineData("file:///c:/windows/win.ini", false)]
    [InlineData("ftp://example.com/file", false)]
    [InlineData("/relative/path", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Only_absolute_http_addresses_are_accepted_for_checking(string? url, bool expected)
    {
        Assert.Equal(expected, SeoHttp.IsHttpUrl(url, out _));
    }

    [Fact]
    public async Task Emailed_links_use_the_configured_site_address_not_the_Host_header()
    {
        await using var site = await TestSite.CreateAsync(new Dictionary<string, string?>
        {
            ["Zauber:SiteUrl"] = "https://www.real-site.test"
        });

        var url = RequestWithHost(site, "attacker.test").ToAbsoluteUrl("/account/resetpassword", new { code = "abc" });

        Assert.Equal("https://www.real-site.test/account/resetpassword?code=abc", url);
    }

    [Fact]
    public async Task Without_a_configured_site_address_links_fall_back_to_the_request()
    {
        await using var site = await TestSite.CreateAsync();

        var url = RequestWithHost(site, "www.real-site.test").ToAbsoluteUrl("/account/resetpassword");

        Assert.Equal("https://www.real-site.test/account/resetpassword", url);
    }

    private static IHttpContextAccessor RequestWithHost(TestSite site, string host)
    {
        var context = new DefaultHttpContext { RequestServices = site.Services };
        context.Request.Scheme = "https";
        context.Request.Host = new HostString(host);
        return new HttpContextAccessor { HttpContext = context };
    }
}
