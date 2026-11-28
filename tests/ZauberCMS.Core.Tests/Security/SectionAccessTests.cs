using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Data.Interfaces;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Interfaces;
using ZauberCMS.Core.Membership.Middleware;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Membership.Parameters;
using ZauberCMS.Core.SeedData;
using ZauberCMS.Core.Sections.Authorization;
using ZauberCMS.Core.Sections.Interfaces;
using ZauberCMS.Core.Tests.Infrastructure;

namespace ZauberCMS.Core.Tests.Security;

/// <summary>
/// Back-office access per section and role (Roadmap D38, D66–D71): which sections a user may open, how the admin's
/// pages are gated, the seeded Editor role, and that someone who manages users and roles cannot give anybody more
/// than they have.
/// </summary>
public class SectionAccessTests : IAsyncLifetime
{
    /// <summary>Overridden by the PostgreSQL and SQL Server copies of this class at the end of the file.</summary>
    protected virtual string DatabaseProvider => TestSite.SqliteProvider;

    private TestSite _site = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _site = await TestSite.CreateAsync(databaseProvider: DatabaseProvider);

    public async ValueTask DisposeAsync() => await _site.DisposeAsync();

    // ---- The Editor role (D69) ----

    [Fact]
    public async Task D69_the_Editor_role_is_seeded_with_the_Content_and_Media_sections()
    {
        var editor = await _site.QueryDatabaseAsync(db =>
            db.Roles.AsNoTracking().SingleAsync(x => x.NormalizedName == "EDITOR", Ct));

        Assert.Equal(Constants.Roles.EditorRoleName, editor.Name);
        Assert.Equal([Constants.Sections.ContentSection, Constants.Sections.MediaSection], editor.GetAllowedSections());
    }

    [Fact]
    public async Task D69_a_deleted_Editor_role_does_not_come_back_at_the_next_start()
    {
        await _site.QueryDatabaseAsync(async db =>
        {
            db.Roles.Remove(await db.Roles.SingleAsync(x => x.NormalizedName == "EDITOR", Ct));
            return await db.SaveChangesAsync(Ct);
        });

        RunSeeder();

        Assert.False(await _site.QueryDatabaseAsync(db => db.Roles.AnyAsync(x => x.NormalizedName == "EDITOR", Ct)));
    }

    [Fact]
    public async Task D69_an_Editor_role_a_site_already_has_is_left_as_it_is()
    {
        // A site from before the seeder, with a role of that name of its own (for front-end members, say)
        await _site.QueryDatabaseAsync(async db =>
        {
            db.GlobalDatas.RemoveRange(db.GlobalDatas.Where(x => x.Alias == SeedEditorRole.MarkerAlias));
            var seeded = await db.Roles.SingleAsync(x => x.NormalizedName == "EDITOR", Ct);
            seeded.ExtendedData = new Dictionary<string, object>();
            seeded.Description = "Members who write for the magazine";
            return await db.SaveChangesAsync(Ct);
        });

        RunSeeder();

        var roles = await _site.QueryDatabaseAsync(db =>
            db.Roles.AsNoTracking().Where(x => x.NormalizedName == "EDITOR").ToListAsync(Ct));
        var role = Assert.Single(roles);
        Assert.Empty(role.GetAllowedSections());
        Assert.Equal("Members who write for the magazine", role.Description);
        Assert.True(await _site.QueryDatabaseAsync(db => db.GlobalDatas.AnyAsync(x => x.Alias == SeedEditorRole.MarkerAlias, Ct)));
    }

    // ---- Which sections a user may open (D66) ----

    [Fact]
    public async Task D66_an_administrator_may_open_every_section_and_nobody_signed_in_none()
    {
        using var scope = _site.CreateScope();
        var access = scope.ServiceProvider.GetRequiredService<IBackOfficeAccessService>();

        var forAdmin = await access.GetSectionsAsync(Principal(Constants.Roles.AdminRoleName), Ct);
        var forNobody = await access.GetSectionsAsync(new ClaimsPrincipal(new ClaimsIdentity()), Ct);
        var forMember = await access.GetSectionsAsync(Principal(Constants.Roles.StandardRoleName), Ct);

        Assert.Equal(access.GetAllSections().Select(x => x.Alias), forAdmin.Select(x => x.Alias));
        Assert.Contains(forAdmin, x => x.Alias == Constants.Sections.SettingsSection);
        Assert.Empty(forNobody);
        Assert.Empty(forMember);
    }

    [Fact]
    public async Task D66_a_user_may_open_the_sections_of_all_their_roles_together_in_section_order()
    {
        await SaveRoleAsync("Designers", Constants.Sections.StructureSection);

        using var scope = _site.CreateScope();
        var access = scope.ServiceProvider.GetRequiredService<IBackOfficeAccessService>();
        var sections = await access.GetSectionsAsync(Principal(Constants.Roles.EditorRoleName, "Designers"), Ct);

        Assert.Equal(
            access.GetAllSections().Select(x => x.Alias)
                .Where(x => x is Constants.Sections.ContentSection or Constants.Sections.MediaSection
                    or Constants.Sections.StructureSection),
            sections.Select(x => x.Alias));
        Assert.False(await access.CanOpenSectionAsync(Principal(Constants.Roles.EditorRoleName),
            Constants.Sections.SettingsSection, Ct));
    }

    [Fact]
    public async Task D66_a_section_given_to_a_role_applies_as_soon_as_the_role_is_saved()
    {
        var role = await SaveRoleAsync("Translators");
        var translator = Principal("Translators");
        using (var scope = _site.CreateScope())
        {
            // Read once, so the list is cached
            Assert.False(await scope.ServiceProvider.GetRequiredService<IBackOfficeAccessService>()
                .CanOpenSectionAsync(translator, Constants.Sections.SettingsSection, Ct));
        }

        role.SetAllowedSections([Constants.Sections.SettingsSection]);
        using (var scope = _site.CreateScope())
        {
            var saved = await scope.ServiceProvider.GetRequiredService<IMembershipService>()
                .SaveRoleAsync(new SaveRoleParameters { Role = role }, Ct);
            Assert.True(saved.Success, TestSite.Describe(saved.Messages));
        }

        using (var scope = _site.CreateScope())
        {
            Assert.True(await scope.ServiceProvider.GetRequiredService<IBackOfficeAccessService>()
                .CanOpenSectionAsync(translator, Constants.Sections.SettingsSection, Ct));
        }
    }

    [Fact]
    public async Task D66_a_section_alias_left_on_a_role_by_a_removed_plugin_opens_nothing()
    {
        await SaveRoleAsync("Old plugin users", "RemovedPluginSection");

        using var scope = _site.CreateScope();
        var access = scope.ServiceProvider.GetRequiredService<IBackOfficeAccessService>();

        Assert.False(await access.CanOpenSectionAsync(Principal("Old plugin users"), "RemovedPluginSection", Ct));
        Assert.Empty(await access.GetSectionsAsync(Principal("Old plugin users"), Ct));
    }

    // ---- Pages, policies and the middleware (D67) ----

    [Theory]
    [InlineData("/admin/content", Constants.Sections.ContentSection)]
    [InlineData("/Admin/Content/updatecontent/9b1f0000-0000-0000-0000-000000000000", Constants.Sections.ContentSection)]
    [InlineData("/admin/media/", Constants.Sections.MediaSection)]
    [InlineData("/admin/users/editrole/1?tab=2", Constants.Sections.UsersSection)]
    [InlineData("http://site.test/admin/settings/languages", Constants.Sections.SettingsSection)]
    [InlineData("/admin/contentx", null)]
    [InlineData("/admin", null)]
    [InlineData("/admin/seo/redirects", null)]
    [InlineData("/content", null)]
    public void D67_an_admin_address_belongs_to_the_section_its_path_is_under(string path, string? expected)
    {
        using var scope = _site.CreateScope();
        var access = scope.ServiceProvider.GetRequiredService<IBackOfficeAccessService>();

        Assert.Equal(expected, access.GetSectionForPath(path)?.Alias);
    }

    [Fact]
    public async Task D67_the_section_policies_let_in_whoever_may_open_the_section()
    {
        using var scope = _site.CreateScope();
        var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
        var content = SectionPolicies.ForSections(Constants.Sections.ContentSection);
        var settings = SectionPolicies.ForSections(Constants.Sections.SettingsSection);
        var editor = Principal(Constants.Roles.EditorRoleName);

        Assert.True((await authorization.AuthorizeAsync(editor, null, content)).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(editor, null, settings)).Succeeded);
        Assert.True((await authorization.AuthorizeAsync(editor, null,
            SectionPolicies.ForSections(Constants.Sections.SettingsSection, Constants.Sections.MediaSection))).Succeeded);
        Assert.True((await authorization.AuthorizeAsync(Principal(Constants.Roles.AdminRoleName), null, settings)).Succeeded);
        Assert.True((await authorization.AuthorizeAsync(editor, null, SectionPolicies.BackOffice)).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(Principal(Constants.Roles.StandardRoleName), null,
            SectionPolicies.BackOffice)).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(new ClaimsPrincipal(new ClaimsIdentity()), null, content)).Succeeded);
    }

    [Fact]
    public void D67_AuthorizeSection_names_the_section_policy()
    {
        var one = new AuthorizeSectionAttribute(Constants.Sections.MediaSection);
        var any = new AuthorizeSectionAttribute();

        Assert.Equal(SectionPolicies.SectionPrefix + Constants.Sections.MediaSection, one.Policy);
        Assert.Equal([Constants.Sections.MediaSection], one.SectionAliases);
        Assert.Equal(SectionPolicies.BackOffice, any.Policy);
        Assert.Empty(any.SectionAliases);
        Assert.Null(SectionPolicies.ParseSections("SomeOtherPolicy"));
    }

    [Theory]
    [InlineData("/admin/content/plugin-tool", Constants.Roles.EditorRoleName, true)]
    [InlineData("/admin/settings/plugin-tool", Constants.Roles.EditorRoleName, false)]
    [InlineData("/admin/plugin-page", Constants.Roles.EditorRoleName, false)]
    [InlineData("/admin/plugin-page", Constants.Roles.AdminRoleName, true)]
    [InlineData("/admin/content/plugin-tool", Constants.Roles.StandardRoleName, false)]
    public async Task D67_an_admin_page_without_its_own_authorisation_is_for_the_users_of_its_section(string path,
        string role, bool expected)
    {
        using var scope = _site.CreateScope();
        var passedOn = false;
        var middleware = new AdminAccessMiddleware(_ =>
        {
            passedOn = true;
            return Task.CompletedTask;
        }, NullLogger<AdminAccessMiddleware>.Instance);
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider, User = Principal(role) };
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(), "plugin page"));

        await middleware.InvokeAsync(context, scope.ServiceProvider.GetRequiredService<IDataService>(),
            scope.ServiceProvider.GetRequiredService<IBackOfficeAccessService>());

        Assert.Equal(expected, passedOn);
    }

    [Theory]
    [InlineData("/admin", Constants.Roles.EditorRoleName, "/admin/content")]
    [InlineData("/Admin/", "Media only", "/admin/media")]
    [InlineData("/admin", Constants.Roles.StandardRoleName, null)]
    public async Task D67_the_admin_address_sends_a_signed_in_user_to_their_first_section(string path, string role,
        string? expected)
    {
        await SaveRoleAsync("Media only", Constants.Sections.MediaSection);
        using var scope = _site.CreateScope();
        var passedOn = false;
        var middleware = new AdminAccessMiddleware(_ =>
        {
            passedOn = true;
            return Task.CompletedTask;
        }, NullLogger<AdminAccessMiddleware>.Instance);
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider, User = Principal(role) };
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = path;
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask,
            new EndpointMetadataCollection(new AuthorizeSectionAttribute()), "landing page"));

        await middleware.InvokeAsync(context, scope.ServiceProvider.GetRequiredService<IDataService>(),
            scope.ServiceProvider.GetRequiredService<IBackOfficeAccessService>());

        if (expected == null)
        {
            // No section: on to the landing page, whose [AuthorizeSection] refuses them
            Assert.True(passedOn);
        }
        else
        {
            Assert.False(passedOn);
            Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
            Assert.Equal(expected, context.Response.Headers.Location.ToString());
        }
    }

    [Fact]
    public void D67_every_admin_page_ZauberCMS_ships_declares_who_may_open_it()
    {
        var pages = typeof(ZauberCMS.Components.Admin.AdminIndex).Assembly.GetExportedTypes()
            .Where(type => typeof(IComponent).IsAssignableFrom(type))
            .Where(type => type.GetCustomAttributes<RouteAttribute>().Any(route => AdminAccess.IsAdminPath(route.Template)))
            .ToList();

        Assert.NotEmpty(pages);
        Assert.All(pages, page => Assert.True(AdminAccess.DeclaresAuthorization(page), page.FullName));

        Assert.Equal([Constants.Sections.ContentSection], SectionsOf<ZauberCMS.Components.Admin.ContentSection.EditContent>());
        Assert.Equal([Constants.Sections.MediaSection], SectionsOf<ZauberCMS.Components.Admin.MediaSection.MediaIndex>());
        Assert.Equal([Constants.Sections.UsersSection], SectionsOf<ZauberCMS.Components.Admin.UsersSection.RoleEditor>());
        Assert.Equal([Constants.Sections.StructureSection], SectionsOf<ZauberCMS.Components.Admin.StructureSection.ContentTypeEditor>());
        Assert.Equal([Constants.Sections.SettingsSection], SectionsOf<ZauberCMS.Components.Seo.SeoRedirects>());
        Assert.Equal([""], SectionsOf<ZauberCMS.Components.Admin.AdminIndex>());
        // ZA-213: a dialog is no page; ChangeTab had a route outside /admin
        Assert.Empty(typeof(ZauberCMS.Components.Admin.StructureSection.Dialogs.ChangeTab).GetCustomAttributes<RouteAttribute>());
        // D70: the global settings are administrators' only, whoever else may open Settings
        Assert.Contains(typeof(ZauberCMS.Components.Admin.SettingsSection.GlobalSettings)
                .GetCustomAttributes<AuthorizeAttribute>(),
            attribute => attribute.Roles == Constants.Roles.AdminRoleName);
    }

    // ---- Managing users and roles without widening anyone's access (D68) ----

    [Fact]
    public async Task D68_someone_who_may_open_Users_cannot_change_an_administrator_but_can_change_anyone_else()
    {
        var (manager, admin, writer, _) = await CreateUserManagerScenarioAsync();

        using var scope = _site.CreateScope();
        var access = scope.ServiceProvider.GetRequiredService<IBackOfficeAccessService>();

        Assert.False((await access.CanEditUserAsync(manager, admin.Id, Ct)).Allowed);
        Assert.True((await access.CanEditUserAsync(manager, writer.Id, Ct)).Allowed);
        Assert.True((await access.CanEditUserAsync(await PrincipalOfAsync(admin), writer.Id, Ct)).Allowed);
        // Without the Users section nobody may change anybody, not even the Editor role's members
        Assert.False((await access.CanEditUserAsync(Principal(Constants.Roles.EditorRoleName), writer.Id, Ct)).Allowed);
    }

    [Fact]
    public async Task ZA203_someone_who_may_open_Users_cannot_change_a_user_who_opens_a_section_they_cannot()
    {
        // Setting the editor's password would hand the manager Media, which they do not have
        var (manager, admin, _, editor) = await CreateUserManagerScenarioAsync();

        using var scope = _site.CreateScope();
        var access = scope.ServiceProvider.GetRequiredService<IBackOfficeAccessService>();

        var refused = await access.CanEditUserAsync(manager, editor.Id, Ct);
        Assert.False(refused.Allowed);
        Assert.Contains("Media", refused.Reason, StringComparison.Ordinal);
        Assert.False((await access.CanSetUserRolesAsync(manager, editor.Id, [], Ct)).Allowed);
        Assert.True((await access.CanEditUserAsync(await PrincipalOfAsync(admin), editor.Id, Ct)).Allowed);
    }

    [Fact]
    public async Task D68_someone_who_may_open_Users_cannot_give_the_Admin_role_or_a_role_they_are_not_in()
    {
        var (manager, _, writer, _) = await CreateUserManagerScenarioAsync();
        var managerId = Guid.Parse(manager.FindFirstValue(ClaimTypes.NameIdentifier)!);

        using var scope = _site.CreateScope();
        var access = scope.ServiceProvider.GetRequiredService<IBackOfficeAccessService>();

        // Not to themselves, and not to anyone else
        Assert.False((await access.CanSetUserRolesAsync(manager, managerId, ["Managers", Constants.Roles.AdminRoleName], Ct)).Allowed);
        Assert.False((await access.CanSetUserRolesAsync(manager, writer.Id, ["Writers", Constants.Roles.AdminRoleName], Ct)).Allowed);
        Assert.False((await access.CanSetUserRolesAsync(manager, writer.Id, ["Writers", "Designers"], Ct)).Allowed);
        Assert.False((await access.CanSetUserRolesAsync(manager, writer.Id, ["Writers", Constants.Roles.EditorRoleName], Ct)).Allowed);
        // A role they are in may be given; taking one away is allowed
        Assert.True((await access.CanSetUserRolesAsync(manager, writer.Id, ["Writers", "Managers"], Ct)).Allowed);
        Assert.True((await access.CanSetUserRolesAsync(manager, writer.Id, [], Ct)).Allowed);
        Assert.False(access.CanGiveRole(manager, Constants.Roles.AdminRoleName));
        Assert.True(access.CanGiveRole(Principal(Constants.Roles.AdminRoleName), Constants.Roles.AdminRoleName));
    }

    [Fact]
    public async Task ZA205_a_manager_whose_role_was_taken_away_cannot_use_the_old_sign_in_to_give_it_back()
    {
        var (manager, _, writer, _) = await CreateUserManagerScenarioAsync();
        var managerId = Guid.Parse(manager.FindFirstValue(ClaimTypes.NameIdentifier)!);
        using (var scope = _site.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var stored = (await userManager.FindByIdAsync(managerId.ToString()))!;
            Assert.True((await userManager.RemoveFromRoleAsync(stored, "Managers")).Succeeded);
        }

        using var check = _site.CreateScope();
        var access = check.ServiceProvider.GetRequiredService<IBackOfficeAccessService>();

        // The sign-in still says "Managers"; the roles as stored do not
        Assert.True(manager.IsInRole("Managers"));
        Assert.False((await access.CanSetUserRolesAsync(manager, managerId, ["Managers"], Ct)).Allowed);
        Assert.False((await access.CanEditUserAsync(manager, writer.Id, Ct)).Allowed);
        Assert.False((await access.CanEditRoleAsync(manager, (await GetRoleAsync("Managers")).Id, Ct)).Allowed);
    }

    [Fact]
    public async Task D68_someone_who_may_open_Users_cannot_widen_their_own_role_or_change_one_they_are_not_in()
    {
        var (manager, _, _, _) = await CreateUserManagerScenarioAsync();
        var managers = await GetRoleAsync("Managers");
        var designers = await GetRoleAsync("Designers");
        var adminRole = await GetRoleAsync(Constants.Roles.AdminRoleName);

        using var scope = _site.CreateScope();
        var access = scope.ServiceProvider.GetRequiredService<IBackOfficeAccessService>();

        managers.SetAllowedSections([Constants.Sections.ContentSection, Constants.Sections.UsersSection, Constants.Sections.SettingsSection]);
        Assert.False((await access.CanSaveRoleAsync(manager, managers, Ct)).Allowed);
        managers.SetAllowedSections([Constants.Sections.UsersSection]);
        Assert.True((await access.CanSaveRoleAsync(manager, managers, Ct)).Allowed);

        Assert.False((await access.CanEditRoleAsync(manager, designers.Id, Ct)).Allowed);
        Assert.False((await access.CanEditRoleAsync(manager, adminRole.Id, Ct)).Allowed);
        Assert.False((await access.CanDeleteRoleAsync(manager, adminRole.Id, Ct)).Allowed);
        Assert.True((await access.CanDeleteRoleAsync(manager, managers.Id, Ct)).Allowed);

        // A new role: only sections they have, never the Admin name
        var newRole = new Role { Name = "Interns" };
        newRole.SetAllowedSections([Constants.Sections.ContentSection]);
        Assert.True((await access.CanSaveRoleAsync(manager, newRole, Ct)).Allowed);
        newRole.SetAllowedSections([Constants.Sections.StructureSection]);
        Assert.False((await access.CanSaveRoleAsync(manager, newRole, Ct)).Allowed);
        Assert.False((await access.CanSaveRoleAsync(manager, new Role { Name = "admin" }, Ct)).Allowed);

        // An administrator may do all of it
        var admin = Principal(Constants.Roles.AdminRoleName);
        Assert.True((await access.CanSaveRoleAsync(admin, newRole, Ct)).Allowed);
        Assert.True((await access.CanEditRoleAsync(admin, adminRole.Id, Ct)).Allowed);
    }

    [Fact]
    public async Task ZA206_the_role_every_registration_gets_opens_no_section()
    {
        await EnsureRoleAsync(Constants.Roles.StandardRoleName);
        var member = await GetRoleAsync(Constants.Roles.StandardRoleName);

        using (var scope = _site.CreateScope())
        {
            var access = scope.ServiceProvider.GetRequiredService<IBackOfficeAccessService>();
            member.SetAllowedSections([Constants.Sections.ContentSection]);
            // Not even an administrator gives it sections
            Assert.False((await access.CanSaveRoleAsync(Principal(Constants.Roles.AdminRoleName), member, Ct)).Allowed);
        }

        // Sections stored on it anyway (by hand, or before Zauber:NewUserStartingRole pointed at it) open nothing
        await _site.QueryDatabaseAsync(async db =>
        {
            var stored = await db.Roles.SingleAsync(x => x.Id == member.Id, Ct);
            stored.SetAllowedSections([Constants.Sections.ContentSection]);
            return await db.SaveChangesAsync(Ct);
        });
        using (var scope = _site.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<ZauberCMS.Core.Shared.Services.ICacheService>()
                .ClearCachedItemsWithPrefix(nameof(Role));
            var access = scope.ServiceProvider.GetRequiredService<IBackOfficeAccessService>();
            Assert.Empty(await access.GetSectionsAsync(Principal(Constants.Roles.StandardRoleName), Ct));
            Assert.False(await access.CanOpenSectionAsync(Principal(Constants.Roles.StandardRoleName),
                Constants.Sections.ContentSection, Ct));
        }
    }

    [Fact]
    public async Task ZA202_a_role_that_restricts_pages_cannot_be_deleted()
    {
        var role = await SaveRoleAsync("Subscribers");
        var pageType = await _site.CreateContentTypeAsync("article");
        var page = await _site.CreateContentAsync(pageType, "Subscribers only");
        using (var scope = _site.CreateScope())
        {
            var restricted = await scope.ServiceProvider.GetRequiredService<ZauberCMS.Core.Content.Interfaces.IContentService>()
                .SetContentRolesAsync(new ZauberCMS.Core.Content.Parameters.SetContentRolesParameters
                {
                    ContentId = page.Id, RoleIds = [role.Id]
                }, Ct);
            Assert.True(restricted.Success, TestSite.Describe(restricted.Messages));
        }

        using (var scope = _site.CreateScope())
        {
            var deleted = await scope.ServiceProvider.GetRequiredService<IMembershipService>()
                .DeleteRoleAsync(new DeleteRoleParameters { Id = role.Id }, Ct);
            Assert.False(deleted.Success);
        }

        // The page stays restricted to the role
        Assert.True(await _site.QueryDatabaseAsync(db => db.ContentRoles.AnyAsync(x => x.RoleId == role.Id, Ct)));
    }

    [Fact]
    public async Task ZA207_saving_a_role_clears_the_restricted_media_lookup()
    {
        var role = await SaveRoleAsync("Readers");
        using (var scope = _site.CreateScope())
        {
            var cache = scope.ServiceProvider.GetRequiredService<ZauberCMS.Core.Shared.Services.ICacheService>();
            await cache.GetSetCachedItemAsync("RestrictedMediaAccess".GenerateCacheKey<ZauberCMS.Core.Media.Models.Media>(),
                () => Task.FromResult("cached"));
        }

        role.Name = "Subscribers";
        using (var scope = _site.CreateScope())
        {
            Assert.True((await scope.ServiceProvider.GetRequiredService<IMembershipService>()
                .SaveRoleAsync(new SaveRoleParameters { Role = role }, Ct)).Success);
        }

        var keys = _site.Services.GetRequiredService<ZauberCMS.Core.Shared.Services.ServerCache>().Keys;
        Assert.DoesNotContain(keys, key => key.StartsWith(nameof(ZauberCMS.Core.Media.Models.Media), StringComparison.Ordinal));
    }

    [Fact]
    public async Task D68_the_Admin_role_cannot_be_renamed_or_deleted()
    {
        await EnsureRoleAsync(Constants.Roles.AdminRoleName);
        var adminRole = await GetRoleAsync(Constants.Roles.AdminRoleName);

        using var scope = _site.CreateScope();
        var membership = scope.ServiceProvider.GetRequiredService<IMembershipService>();
        adminRole.Name = "Administrators";
        var renamed = await membership.SaveRoleAsync(new SaveRoleParameters { Role = adminRole }, Ct);
        var deleted = await membership.DeleteRoleAsync(new DeleteRoleParameters { Id = adminRole.Id }, Ct);

        Assert.False(renamed.Success);
        Assert.False(deleted.Success);
        Assert.Equal(Constants.Roles.AdminRoleName, (await GetRoleAsync(Constants.Roles.AdminRoleName)).Name);
    }

    [Fact]
    public async Task ZA200_a_role_is_deleted_by_its_id_and_by_the_old_RoleId_the_role_editor_set()
    {
        var byId = await SaveRoleAsync("Deleted by Id");
        var byRoleId = await SaveRoleAsync("Deleted by RoleId");

        using (var scope = _site.CreateScope())
        {
            var membership = scope.ServiceProvider.GetRequiredService<IMembershipService>();
            var first = await membership.DeleteRoleAsync(new DeleteRoleParameters { Id = byId.Id }, Ct);
#pragma warning disable CS0618 // the obsolete name is the one under test
            var second = await membership.DeleteRoleAsync(new DeleteRoleParameters { RoleId = byRoleId.Id }, Ct);
#pragma warning restore CS0618
            Assert.True(first.Success, TestSite.Describe(first.Messages));
            Assert.True(second.Success, TestSite.Describe(second.Messages));
        }

        Assert.False(await _site.QueryDatabaseAsync(db =>
            db.Roles.AnyAsync(x => x.Id == byId.Id || x.Id == byRoleId.Id, Ct)));
    }

    [Fact]
    public async Task D67_signing_in_sends_someone_who_may_open_a_section_to_the_admin()
    {
        var editor = await _site.CreateUserAsync("signing-in-editor");
        var member = await _site.CreateUserAsync("signing-in-member");
        await AddToRolesAsync(editor, Constants.Roles.EditorRoleName);
        await EnsureRoleAsync(Constants.Roles.StandardRoleName);
        await AddToRolesAsync(member, Constants.Roles.StandardRoleName);

        var editorResult = await LoginAsync(editor.Email!);
        var memberResult = await LoginAsync(member.Email!);

        Assert.True(editorResult.Success, TestSite.Describe(editorResult.Messages));
        Assert.Equal(Urls.AdminBaseUrl, editorResult.NavigateToUrl);
        Assert.True(memberResult.Success, TestSite.Describe(memberResult.Messages));
        Assert.NotEqual(Urls.AdminBaseUrl, memberResult.NavigateToUrl);
    }

    // ---- Helpers ----

    /// <summary>
    /// A "Managers" role with Content and Users and a manager in it; an administrator; a writer in "Writers"
    /// (Content); an editor in Editor (Content and Media); a "Designers" role with Structure. Returns the manager's
    /// principal as their sign-in makes it.
    /// </summary>
    private async Task<(ClaimsPrincipal Manager, User Admin, User Writer, User Editor)> CreateUserManagerScenarioAsync()
    {
        await SaveRoleAsync("Managers", Constants.Sections.ContentSection, Constants.Sections.UsersSection);
        await SaveRoleAsync("Writers", Constants.Sections.ContentSection);
        await SaveRoleAsync("Designers", Constants.Sections.StructureSection);
        await EnsureRoleAsync(Constants.Roles.AdminRoleName);

        var manager = await _site.CreateUserAsync("manager");
        var admin = await _site.CreateUserAsync("an-admin");
        var writer = await _site.CreateUserAsync("writer");
        var editor = await _site.CreateUserAsync("an-editor");
        await AddToRolesAsync(manager, "Managers");
        await AddToRolesAsync(admin, Constants.Roles.AdminRoleName);
        await AddToRolesAsync(writer, "Writers");
        await AddToRolesAsync(editor, Constants.Roles.EditorRoleName);

        return (await PrincipalOfAsync(manager), admin, writer, editor);
    }

    private async Task<Role> SaveRoleAsync(string name, params string[] sections)
    {
        var role = new Role { Name = name };
        role.SetAllowedSections(sections);
        using var scope = _site.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<IMembershipService>()
            .SaveRoleAsync(new SaveRoleParameters { Role = role }, Ct);
        Assert.True(result.Success, TestSite.Describe(result.Messages));
        return result.Entity!;
    }

    private async Task EnsureRoleAsync(string name)
    {
        using var scope = _site.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();
        if (!await roleManager.RoleExistsAsync(name))
        {
            Assert.True((await roleManager.CreateAsync(new Role { Name = name })).Succeeded);
        }
    }

    private async Task<Role> GetRoleAsync(string name) =>
        await _site.QueryDatabaseAsync(db => db.Roles.AsNoTracking().SingleAsync(x => x.Name == name, Ct));

    private async Task AddToRolesAsync(User user, params string[] roles)
    {
        using var scope = _site.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var stored = (await userManager.FindByIdAsync(user.Id.ToString()))!;
        var result = await userManager.AddToRolesAsync(stored, roles);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(e => e.Description)));
    }

    private async Task<ClaimsPrincipal> PrincipalOfAsync(User user)
    {
        using var scope = _site.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var factory = scope.ServiceProvider.GetRequiredService<IUserClaimsPrincipalFactory<User>>();
        return await factory.CreateAsync((await userManager.FindByIdAsync(user.Id.ToString()))!);
    }

    private async Task<AuthenticationResult> LoginAsync(string email)
    {
        using var scope = _site.CreateScope();
        // Sign-in writes cookies, so it needs a request to write them to
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext =
            new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        return await scope.ServiceProvider.GetRequiredService<IMembershipService>().LoginUserAsync(
            new LoginUserParameters { Email = email, Password = "Passw0rd-For-Tests" }, Ct);
    }

    private void RunSeeder()
    {
        using var scope = _site.CreateScope();
        new SeedEditorRole().Initialise(scope.ServiceProvider.GetRequiredService<IZauberDbContext>());
    }

    private static string[] SectionsOf<T>() => typeof(T).GetCustomAttributes<AuthorizeSectionAttribute>()
        .Select(attribute => attribute.SectionAliases.Count == 0 ? "" : string.Join(",", attribute.SectionAliases))
        .ToArray();

    private static ClaimsPrincipal Principal(params string[] roles) => new(new ClaimsIdentity(
        [new Claim(ClaimTypes.Name, "someone"), .. roles.Select(role => new Claim(ClaimTypes.Role, role))], "Test"));
}

/// <summary>The same tests on a real PostgreSQL (Roadmap D15); skipped without Docker.</summary>
public class SectionAccessTestsOnPostgreSql : SectionAccessTests
{
    protected override string DatabaseProvider => TestSite.PostgreSqlProvider;
}

/// <summary>The same tests on SQL Server; skipped without <c>ZAUBER_TEST_SQLSERVER</c>.</summary>
public class SectionAccessTestsOnSqlServer : SectionAccessTests
{
    protected override string DatabaseProvider => TestSite.SqlServerProvider;
}
