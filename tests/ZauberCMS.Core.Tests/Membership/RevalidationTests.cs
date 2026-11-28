using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Membership;
using ZauberCMS.Core.Membership.Models;

namespace ZauberCMS.Core.Tests.Membership;

/// <summary>
/// An open admin tab's sign-in is checked every ten minutes. A check that cannot read the user (the database is
/// down a moment) keeps the sign-in instead of signing the tab out, which would lose unsaved work (ZA-215).
/// </summary>
public class RevalidationTests
{
    [Fact]
    public async Task ZA215_a_check_that_cannot_read_the_user_keeps_the_sign_in()
    {
        var services = new ServiceCollection();
        services.AddScoped<UserManager<User>>(_ => throw new InvalidOperationException("The database is down"));
        await using var provider = services.BuildServiceProvider();
        var revalidating = new IdentityRevalidatingAuthenticationStateProvider(NullLoggerFactory.Instance,
            provider.GetRequiredService<IServiceScopeFactory>(), Options.Create(new IdentityOptions()));
        var state = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())], "Test")));

        var validate = typeof(IdentityRevalidatingAuthenticationStateProvider).GetMethod(
            "ValidateAuthenticationStateAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var stillValid = await (Task<bool>)validate.Invoke(revalidating, [state, TestContext.Current.CancellationToken])!;

        Assert.True(stillValid);
        ((IDisposable)revalidating).Dispose();
    }
}
