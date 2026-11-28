using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ZauberCMS.Components.Controllers;
using ZauberCMS.Core.Membership;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Tests.Infrastructure;

namespace ZauberCMS.Core.Tests.Membership;

/// <summary>
/// After a user changes their own password (or anything else that replaces the security stamp), the browser's
/// sign-in is renewed through the refresh endpoint with the ticket the change handed out, also when the cookie is
/// older than the stamp check's five minutes, which refuses it before the endpoint runs (ZA-218, Roadmap D72).
/// </summary>
public class RefreshSignInTests : IAsyncLifetime
{
    private TestSite _site = null!;

    public async ValueTask InitializeAsync() => _site = await TestSite.CreateAsync();

    public async ValueTask DisposeAsync() => await _site.DisposeAsync();

    [Fact]
    public async Task ZA218_a_refused_cookie_is_renewed_with_the_ticket_for_its_own_user()
    {
        var user = await _site.CreateUserAsync("changer");
        using var scope = _site.CreateScope();
        var (cookie, ticket, newStamp) = await StaleCookieAndTicketAsync(scope, user, ticketFor: user);

        var (result, context) = await RefreshAsync(scope, cookie, ticket);

        Assert.Equal("/admin/content", Assert.IsType<RedirectResult>(result).Url);
        var renewed = RenewedCookiePrincipal(scope, context);
        Assert.Equal(user.Id.ToString(), renewed?.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal(newStamp, renewed?.FindFirstValue(
            scope.ServiceProvider.GetRequiredService<IOptions<IdentityOptions>>().Value.ClaimsIdentity.SecurityStampClaimType));
    }

    [Fact]
    public async Task ZA218_a_refused_cookie_is_not_renewed_with_another_users_ticket_or_none()
    {
        var user = await _site.CreateUserAsync("victim");
        var other = await _site.CreateUserAsync("other");
        using var scope = _site.CreateScope();
        var (cookie, otherTicket, _) = await StaleCookieAndTicketAsync(scope, user, ticketFor: other);

        foreach (var ticket in new[] { otherTicket, null })
        {
            var (result, context) = await RefreshAsync(scope, cookie, ticket);

            Assert.Equal(ZauberCMS.Core.Urls.Account.Login, Assert.IsType<RedirectResult>(result).Url);
            Assert.Null(RenewedCookiePrincipal(scope, context));
        }
    }

    /// <summary>A cookie for <paramref name="user"/> issued ten minutes ago, before their stamp changed, and a
    /// ticket made after the change for <paramref name="ticketFor"/>.</summary>
    private static async Task<(string Cookie, string Ticket, string NewStamp)> StaleCookieAndTicketAsync(
        IServiceScope scope, User user, User ticketFor)
    {
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var claimsFactory = scope.ServiceProvider.GetRequiredService<IUserClaimsPrincipalFactory<User>>();
        var stored = (await userManager.FindByIdAsync(user.Id.ToString()))!;
        var oldPrincipal = await claimsFactory.CreateAsync(stored);

        Assert.True((await userManager.UpdateSecurityStampAsync(stored)).Succeeded);
        var ticketUser = (await userManager.FindByIdAsync(ticketFor.Id.ToString()))!;
        if (ticketUser.Id != stored.Id)
        {
            Assert.True((await userManager.UpdateSecurityStampAsync(ticketUser)).Succeeded);
        }

        var options = CookieOptions(scope);
        var cookie = options.TicketDataFormat.Protect(new AuthenticationTicket(oldPrincipal,
            new AuthenticationProperties
            {
                IssuedUtc = DateTimeOffset.UtcNow.AddMinutes(-10),
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1),
                IsPersistent = true
            }, IdentityConstants.ApplicationScheme));
        var ticket = scope.ServiceProvider.GetRequiredService<RefreshSignInTicketService>().Create(ticketUser);
        return (cookie, ticket, stored.SecurityStamp!);
    }

    /// <summary>Calls the endpoint as the request reaches it once the stamp check has refused the cookie: nobody
    /// signed in, the cookie still in the request.</summary>
    private static async Task<(IActionResult Result, HttpContext Context)> RefreshAsync(IServiceScope scope,
        string cookie, string? ticket)
    {
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Request.Headers.Cookie = $"{CookieOptions(scope).Cookie.Name}={cookie}";
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = context;

        var controller = ActivatorUtilities.CreateInstance<AuthController>(scope.ServiceProvider);
        controller.ControllerContext = new ControllerContext { HttpContext = context };
        controller.Url = new UrlHelper(new ActionContext(context, new RouteData(), new ActionDescriptor()));
        return (await controller.RefreshSignIn("/admin/content", ticket), context);
    }

    /// <summary>The principal of the application cookie the response sets, or null when it sets none.</summary>
    private static ClaimsPrincipal? RenewedCookiePrincipal(IServiceScope scope, HttpContext context)
    {
        var options = CookieOptions(scope);
        var prefix = options.Cookie.Name + "=";
        var setCookie = context.Response.Headers.SetCookie
            .Select(header => header?.Split(';')[0])
            .LastOrDefault(value => value != null && value.StartsWith(prefix, StringComparison.Ordinal));
        var value = setCookie?[prefix.Length..];
        return string.IsNullOrEmpty(value) ? null : options.TicketDataFormat.Unprotect(value)?.Principal;
    }

    private static CookieAuthenticationOptions CookieOptions(IServiceScope scope) => scope.ServiceProvider
        .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(IdentityConstants.ApplicationScheme);
}
