using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ZauberCMS.Components.Pages;
using ZauberCMS.Components.Tests.Shared;
using ZauberCMS.Core.Settings;

namespace ZauberCMS.Components.Tests.Account;

/// <summary>
/// Roadmap D95: the account pages and the fallback site pages are site-facing and are in the request's culture
/// (UseRequestLocalization: the site's languages), so their text follows it. The default account layout leaves it
/// as it is; it used to apply the admin's culture in its OnInitialized, which never reached the render.
/// </summary>
public class AccountLayoutCultureTests
{
    [Fact]
    public async Task The_account_layout_renders_its_page_in_the_request_s_culture_not_the_admin_s()
    {
        var previous = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        try
        {
            // The request asked for French (UseRequestLocalization); the admin is in English
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr");

            var html = await AdminRenderer.RenderAsync<LayoutView>(
                services => services.AddSingleton(Options.Create(new ZauberSettings { AdminDefaultLanguage = "en" })),
                new Dictionary<string, object?>
                {
                    [nameof(LayoutView.Layout)] = typeof(BlankLayout),
                    [nameof(LayoutView.ChildContent)] = (RenderFragment)(page =>
                        page.AddContent(0, $"culture:{CultureInfo.CurrentUICulture.Name}/{CultureInfo.CurrentCulture.Name}"))
                });

            Assert.Contains("culture:fr/fr", html, StringComparison.Ordinal);
        }
        finally
        {
            (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = previous;
        }
    }
}
