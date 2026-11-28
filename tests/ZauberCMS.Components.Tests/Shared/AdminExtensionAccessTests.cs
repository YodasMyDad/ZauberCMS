using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Components.Admin.Shared;
using ZauberCMS.Core;

namespace ZauberCMS.Components.Tests.Shared;

/// <summary>
/// ZA-209: an admin extension (nav item, group action, dashboard, tree context menu) whose type carries
/// <c>[Authorize]</c> is shown only to users who pass it; one without is shown to everyone who opens its section.
/// </summary>
public class AdminExtensionAccessTests
{
    private readonly ServiceProvider _services = new ServiceCollection().AddLogging().AddAuthorization()
        .BuildServiceProvider();

    [Fact]
    public async Task ZA209_an_extension_with_Authorize_is_for_the_users_who_pass_it()
    {
        Assert.True(await MayUseAsync(typeof(AdministratorsOnlyTool), User(Constants.Roles.AdminRoleName)));
        Assert.False(await MayUseAsync(typeof(AdministratorsOnlyTool), User(Constants.Roles.EditorRoleName)));
    }

    [Fact]
    public async Task An_extension_without_Authorize_is_for_everyone()
    {
        Assert.True(await MayUseAsync(typeof(OpenTool), User(Constants.Roles.EditorRoleName)));
    }

    private Task<bool> MayUseAsync(Type type, ClaimsPrincipal user) =>
        AdminExtensionAccess.MayUseAsync(type, user, _services.GetRequiredService<IAuthorizationService>(),
            _services.GetRequiredService<IAuthorizationPolicyProvider>());

    private static ClaimsPrincipal User(string role) => new(new ClaimsIdentity(
        [new Claim(ClaimTypes.Name, "someone"), new Claim(ClaimTypes.Role, role)], "Test"));

    [Authorize(Roles = Constants.Roles.AdminRoleName)]
    private sealed class AdministratorsOnlyTool;

    private sealed class OpenTool;
}
