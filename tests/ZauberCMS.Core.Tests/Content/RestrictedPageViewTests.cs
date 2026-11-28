using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ZauberCMS.Components.Pages;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Parameters;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Tests.Infrastructure;

namespace ZauberCMS.Core.Tests.Content;

/// <summary>
/// ZA-188: a page restricted to a role is shown to no one else, whichever view would show it; also when the page's
/// view is missing and the MissingView (which a site may replace with one that renders the whole page) would.
/// </summary>
public class RestrictedPageViewTests : IAsyncLifetime
{
    private const string PageName = "Members only report";

    private TestSite _site = null!;

    public async ValueTask InitializeAsync() => _site = await TestSite.CreateAsync();

    public async ValueTask DisposeAsync() => await _site.DisposeAsync();

    [Fact]
    public async Task ZA188_a_restricted_page_whose_view_is_missing_is_not_rendered_for_a_visitor_outside_its_roles()
    {
        var url = await CreateRestrictedPageWithoutViewAsync();
        _site.SignIn(null);

        var (html, navigatedTo) = await RenderAsync(url);

        Assert.DoesNotContain(PageName, html, StringComparison.Ordinal);
        Assert.NotNull(navigatedTo);
    }

    [Fact]
    public async Task A_restricted_page_whose_view_is_missing_shows_the_MissingView_to_a_visitor_in_its_role()
    {
        // The control: the same page and view are shown once access is settled
        var url = await CreateRestrictedPageWithoutViewAsync();
        _site.Services.GetRequiredService<TestSignIn>().Principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "member"), new Claim(ClaimTypes.Role, "Members")], "Test"));

        var (html, navigatedTo) = await RenderAsync(url);

        Assert.Contains(PageName, html, StringComparison.Ordinal);
        Assert.Null(navigatedTo);
    }

    private async Task<string> CreateRestrictedPageWithoutViewAsync()
    {
        var pageType = await _site.CreateContentTypeAsync("report");
        // No view chosen: the page is shown through Zauber:MissingView, which names the page
        var page = await _site.CreateContentAsync(pageType, PageName);
        using var scope = _site.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();
        Assert.True((await roleManager.CreateAsync(new Role { Name = "Members" })).Succeeded);
        var members = (await roleManager.FindByNameAsync("Members"))!;
        var restricted = await scope.ServiceProvider.GetRequiredService<IContentService>().SetContentRolesAsync(
            new SetContentRolesParameters { ContentId = page.Id, RoleIds = [members.Id] },
            TestContext.Current.CancellationToken);
        Assert.True(restricted.Success, TestSite.Describe(restricted.Messages));
        return page.Url!;
    }

    /// <summary>Renders <see cref="EntryPage"/> for the address as a static page does; returns the HTML (empty when
    /// the render was ended by a redirect) and where it was sent, if anywhere.</summary>
    private async Task<(string Html, string? NavigatedTo)> RenderAsync(string url)
    {
        using var scope = _site.CreateScope();
        var navigation = scope.ServiceProvider.GetRequiredService<NavigationManager>();
        ((IHostEnvironmentNavigationManager)navigation).Initialize("http://localhost/", $"http://localhost/{url}");
        string? navigatedTo = null;
        navigation.LocationChanged += (_, args) => navigatedTo = args.Location;

        await using var renderer = new HtmlRenderer(scope.ServiceProvider, NullLoggerFactory.Instance);
        try
        {
            var html = await renderer.Dispatcher.InvokeAsync(async () =>
            {
                var output = await renderer.RenderComponentAsync<EntryPage>(ParameterView.FromDictionary(
                    new Dictionary<string, object?> { [nameof(EntryPage.Slug)] = url }));
                return output.ToHtmlString();
            });
            return (html, navigatedTo ?? NavigationRequested(navigation, url));
        }
        catch (NavigationException redirect)
        {
            // A static render ends with a redirect: nothing of the page was written
            return (string.Empty, redirect.Location);
        }
    }

    // Without a NavigationException the static navigation manager keeps the address it was sent to
    private static string? NavigationRequested(NavigationManager navigation, string url) =>
        navigation.Uri.EndsWith("/" + url, StringComparison.OrdinalIgnoreCase) ? null : navigation.Uri;
}
