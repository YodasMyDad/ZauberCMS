using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Plugins;
using ZauberCMS.Core.Sections.Interfaces;
using ZauberCMS.Core.Sections.Models;
using ZauberCMS.Core.Settings;
using ZauberCMS.Core.Shared.Services;

namespace ZauberCMS.Core.Sections.Services;

/// <summary>
/// The default <see cref="IBackOfficeAccessService"/> (Roadmap D66–D68, D71). A role's sections are stored on the
/// role (<see cref="RoleExtensions.GetAllowedSections"/>) and read once per server into the cache, under the
/// <see cref="Role"/> prefix, which every role save and delete clears. The role new registrations get
/// (<c>Zauber:NewUserStartingRole</c>) opens no section, whatever is stored on it (D71).
/// <para>
/// Which sections a user may open (the pages, the navigation) is decided from the roles their sign-in carries, as
/// every other role check is. A change that could give access (users, roles, sections) is decided from the editor's
/// roles as stored now, so a user whose roles were just taken away cannot use the old sign-in to give them back.
/// </para>
/// </summary>
public class BackOfficeAccessService(
    ExtensionManager extensionManager,
    ICacheService cacheService,
    IServiceScopeFactory serviceScopeFactory,
    IOptions<ZauberSettings> settings) : IBackOfficeAccessService
{
    /// <summary>The cache key of the role → sections list; under the <see cref="Role"/> prefix.</summary>
    public const string RoleSectionsCacheKey = nameof(Role) + "-AllowedSections";

    private IReadOnlyList<ISection>? _sections;

    /// <summary>The role every new registration gets (<c>Zauber:NewUserStartingRole</c>, default <c>Member</c>).</summary>
    protected string StartingRoleName => settings.Value.NewUserStartingRole ?? Constants.Roles.StandardRoleName;

    public virtual IReadOnlyList<ISection> GetAllSections() =>
        _sections ??= extensionManager.GetInstances<ISection>(true).Values
            .OrderBy(section => section.SortOrder)
            .ThenBy(section => section.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public virtual async Task<IReadOnlyList<ISection>> GetSectionsAsync(ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        if (!IsSignedIn(user))
        {
            return [];
        }

        var all = GetAllSections();
        if (IsAdministrator(user))
        {
            return all;
        }

        var allowed = await GetAllowedAliasesAsync(RoleNamesOf(user));
        return all.Where(section => allowed.Contains(section.Alias)).ToList();
    }

    public virtual async Task<bool> CanOpenSectionAsync(ClaimsPrincipal user, string sectionAlias,
        CancellationToken cancellationToken = default)
    {
        if (!IsSignedIn(user) || string.IsNullOrWhiteSpace(sectionAlias))
        {
            return false;
        }

        return IsAdministrator(user) || IsSection(sectionAlias) &&
            (await GetAllowedAliasesAsync(RoleNamesOf(user))).Contains(sectionAlias);
    }

    public virtual ISection? GetSectionForPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var requestPath = ToPath(path);
        ISection? best = null;
        var bestLength = -1;
        foreach (var section in GetAllSections())
        {
            var indexPath = ToPath(section.IndexUrl);
            // A section at /admin itself (or outside it) would own every admin page: it owns none
            if (!indexPath.HasValue || indexPath.Equals(Urls.AdminBaseUrl, StringComparison.OrdinalIgnoreCase) ||
                !indexPath.StartsWithSegments(Urls.AdminBaseUrl, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (requestPath.StartsWithSegments(indexPath, StringComparison.OrdinalIgnoreCase) &&
                indexPath.Value!.Length > bestLength)
            {
                best = section;
                bestLength = indexPath.Value.Length;
            }
        }

        return best;
    }

    public virtual bool CanGiveRole(ClaimsPrincipal editor, string? roleName)
    {
        if (!IsSignedIn(editor) || string.IsNullOrWhiteSpace(roleName))
        {
            return false;
        }

        if (IsAdministrator(editor))
        {
            return true;
        }

        return !IsAdminRoleName(roleName) && editor.IsInRole(roleName);
    }

    public virtual async Task<BackOfficeAccessResult> CanEditUserAsync(ClaimsPrincipal editor, Guid userId,
        CancellationToken cancellationToken = default)
    {
        var facts = await GetEditorAsync(editor, cancellationToken);
        if (facts.Refused != null)
        {
            return facts.Refused;
        }

        if (facts.IsAdministrator)
        {
            return BackOfficeAccessResult.Allow;
        }

        var targetRoles = await GetStoredRoleNamesAsync(userId, cancellationToken);
        if (targetRoles.Any(IsAdminRoleName))
        {
            return BackOfficeAccessResult.Deny("Only an administrator can change an administrator.");
        }

        // Signing in as someone else must not open more than the editor has: their password, email and roles are
        // theirs to change only when every section they open is one the editor opens too
        var targetSections = await GetAllowedAliasesAsync(targetRoles);
        var notYours = targetSections.Where(alias => IsSection(alias) && !facts.Sections.Contains(alias)).ToList();
        return notYours.Count == 0
            ? BackOfficeAccessResult.Allow
            : BackOfficeAccessResult.Deny(
                $"You can only change users whose sections you can open yourself: {string.Join(", ", notYours.Select(SectionName))}.");
    }

    public virtual async Task<BackOfficeAccessResult> CanSetUserRolesAsync(ClaimsPrincipal editor, Guid userId,
        IEnumerable<string> roleNames, CancellationToken cancellationToken = default)
    {
        var canEdit = await CanEditUserAsync(editor, userId, cancellationToken);
        if (!canEdit.Allowed)
        {
            return canEdit;
        }

        var facts = await GetEditorAsync(editor, cancellationToken);
        if (facts.IsAdministrator)
        {
            return BackOfficeAccessResult.Allow;
        }

        var current = await GetStoredRoleNamesAsync(userId, cancellationToken);
        var added = roleNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Where(name => !current.Contains(name, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var notYours = added
            .Where(name => IsAdminRoleName(name) || !facts.RoleNames.Contains(name, StringComparer.OrdinalIgnoreCase))
            .ToList();
        return notYours.Count == 0
            ? BackOfficeAccessResult.Allow
            : BackOfficeAccessResult.Deny(
                $"You can only give roles you are in yourself, and never the {Constants.Roles.AdminRoleName} role: {string.Join(", ", notYours)}.");
    }

    public virtual async Task<BackOfficeAccessResult> CanEditRoleAsync(ClaimsPrincipal editor, Guid roleId,
        CancellationToken cancellationToken = default)
    {
        var facts = await GetEditorAsync(editor, cancellationToken);
        if (facts.Refused != null)
        {
            return facts.Refused;
        }

        if (facts.IsAdministrator)
        {
            return BackOfficeAccessResult.Allow;
        }

        var storedName = await GetStoredRoleNameAsync(roleId, cancellationToken);
        if (storedName == null)
        {
            // Not saved yet: a new role
            return BackOfficeAccessResult.Allow;
        }

        if (IsAdminRoleName(storedName))
        {
            return BackOfficeAccessResult.Deny($"Only an administrator can change the {Constants.Roles.AdminRoleName} role.");
        }

        return facts.RoleNames.Contains(storedName, StringComparer.OrdinalIgnoreCase)
            ? BackOfficeAccessResult.Allow
            : BackOfficeAccessResult.Deny($"You can only change roles you are in yourself; you are not in {storedName}.");
    }

    public virtual async Task<BackOfficeAccessResult> CanSaveRoleAsync(ClaimsPrincipal editor, Role role,
        CancellationToken cancellationToken = default)
    {
        var canEdit = await CanEditRoleAsync(editor, role.Id, cancellationToken);
        if (!canEdit.Allowed)
        {
            return canEdit;
        }

        // Everyone who registers gets the starting role: it opens no section, for anyone (D71)
        if (string.Equals(role.Name?.Trim(), StartingRoleName, StringComparison.OrdinalIgnoreCase) &&
            role.GetAllowedSections().Count > 0)
        {
            return BackOfficeAccessResult.Deny(
                $"Everyone who registers gets the {StartingRoleName} role (Zauber:NewUserStartingRole), so it cannot open sections of the admin. Give them to another role.");
        }

        var facts = await GetEditorAsync(editor, cancellationToken);
        if (facts.IsAdministrator)
        {
            return BackOfficeAccessResult.Allow;
        }

        if (IsAdminRoleName(role.Name))
        {
            return BackOfficeAccessResult.Deny($"Only an administrator can make the {Constants.Roles.AdminRoleName} role.");
        }

        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var stored = await dbContext.Roles.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == role.Id, cancellationToken);
        var before = stored?.GetAllowedSections() ?? [];
        var notYours = role.GetAllowedSections()
            .Where(alias => !before.Contains(alias, StringComparer.OrdinalIgnoreCase))
            .Where(alias => !facts.Sections.Contains(alias))
            .ToList();

        return notYours.Count == 0
            ? BackOfficeAccessResult.Allow
            : BackOfficeAccessResult.Deny(
                $"You can only give a role sections you can open yourself: {string.Join(", ", notYours.Select(SectionName))}.");
    }

    public virtual Task<BackOfficeAccessResult> CanDeleteRoleAsync(ClaimsPrincipal editor, Guid roleId,
        CancellationToken cancellationToken = default) =>
        CanEditRoleAsync(editor, roleId, cancellationToken);

    /// <summary>True when <paramref name="user"/>'s sign-in carries the Admin role.</summary>
    protected static bool IsAdministrator(ClaimsPrincipal user) => user.IsInRole(Constants.Roles.AdminRoleName);

    /// <summary>True for a signed-in user.</summary>
    protected static bool IsSignedIn(ClaimsPrincipal? user) => user?.Identity?.IsAuthenticated == true;

    /// <summary>
    /// The section aliases of <paramref name="roleNames"/>, put together (without case). The starting role adds
    /// none (D71).
    /// </summary>
    protected virtual async Task<HashSet<string>> GetAllowedAliasesAsync(IEnumerable<string> roleNames)
    {
        var roleSections = await GetRoleSectionsAsync();
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var roleName in roleNames)
        {
            if (roleSections.TryGetValue(roleName, out var aliases))
            {
                allowed.UnionWith(aliases);
            }
        }

        return allowed;
    }

    /// <summary>
    /// Every role with sections, by name (without case): from the cache, read from the database on a miss. The
    /// starting role is left out (D71).
    /// </summary>
    protected virtual async Task<IReadOnlyDictionary<string, string[]>> GetRoleSectionsAsync()
    {
        var startingRole = StartingRoleName;
        var map = await cacheService.GetSetCachedItemAsync(RoleSectionsCacheKey, async () =>
        {
            using var scope = serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
            var roles = await dbContext.Roles.AsNoTracking().ToListAsync();
            IReadOnlyDictionary<string, string[]> result = roles
                .Where(role => !string.IsNullOrEmpty(role.Name))
                .Select(role => (Name: role.Name!, Sections: role.GetAllowedSections().ToArray()))
                .Where(entry => entry.Sections.Length > 0)
                .GroupBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.SelectMany(entry => entry.Sections).ToArray(),
                    StringComparer.OrdinalIgnoreCase);
            return result;
        });
        return (map ?? new Dictionary<string, string[]>())
            .Where(entry => !string.Equals(entry.Key, startingRole, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>The role names <paramref name="user"/>'s sign-in carries.</summary>
    protected static IEnumerable<string> RoleNamesOf(ClaimsPrincipal user) =>
        user.Identities
            .SelectMany(identity => identity.FindAll(identity.RoleClaimType))
            .Select(claim => claim.Value)
            .Where(value => !string.IsNullOrEmpty(value))
            .Distinct(StringComparer.Ordinal);

    /// <summary>
    /// What an editor may give, from their roles as stored now (their sign-in's when it names no user). Refused when
    /// they are not signed in or may not open the Users section.
    /// </summary>
    protected virtual async Task<EditorFacts> GetEditorAsync(ClaimsPrincipal editor, CancellationToken cancellationToken)
    {
        if (!IsSignedIn(editor))
        {
            return new EditorFacts([], false, new HashSet<string>(), BackOfficeAccessResult.Deny("Sign in first."));
        }

        var userId = editor.FindFirstValue(ClaimTypes.NameIdentifier);
        IReadOnlyList<string> roleNames = Guid.TryParse(userId, out var id)
            ? await GetStoredRoleNamesAsync(id, cancellationToken)
            : RoleNamesOf(editor).ToList();
        var isAdministrator = roleNames.Any(IsAdminRoleName);
        var sections = isAdministrator
            ? new HashSet<string>(GetAllSections().Select(section => section.Alias), StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>((await GetAllowedAliasesAsync(roleNames)).Where(IsSection),
                StringComparer.OrdinalIgnoreCase);
        var refused = isAdministrator || sections.Contains(Constants.Sections.UsersSection)
            ? null
            : BackOfficeAccessResult.Deny("You cannot open the Users section.");
        return new EditorFacts(roleNames, isAdministrator, sections, refused);
    }

    /// <summary>An editor's roles as stored, whether they are an administrator, the sections they open, and why they
    /// may change no user or role (null when they may).</summary>
    protected sealed record EditorFacts(IReadOnlyList<string> RoleNames, bool IsAdministrator,
        IReadOnlySet<string> Sections, BackOfficeAccessResult? Refused);

    private async Task<IReadOnlyList<string>> GetStoredRoleNamesAsync(Guid userId, CancellationToken cancellationToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var names = await dbContext.UserRoles.AsNoTracking()
            .Where(userRole => userRole.UserId == userId && !userRole.User.Deleted)
            .Select(userRole => userRole.Role.Name)
            .ToListAsync(cancellationToken);
        return names.OfType<string>().ToList();
    }

    private async Task<string?> GetStoredRoleNameAsync(Guid roleId, CancellationToken cancellationToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        return await dbContext.Roles.AsNoTracking()
            .Where(role => role.Id == roleId)
            .Select(role => role.Name)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private bool IsSection(string alias) =>
        GetAllSections().Any(section => string.Equals(section.Alias, alias, StringComparison.OrdinalIgnoreCase));

    private string SectionName(string alias) =>
        GetAllSections().FirstOrDefault(section =>
            string.Equals(section.Alias, alias, StringComparison.OrdinalIgnoreCase))?.Name ?? alias;

    private static bool IsAdminRoleName(string? roleName) =>
        string.Equals(roleName?.Trim(), Constants.Roles.AdminRoleName, StringComparison.OrdinalIgnoreCase);

    private static PathString ToPath(string? pathOrUrl)
    {
        if (string.IsNullOrWhiteSpace(pathOrUrl))
        {
            return PathString.Empty;
        }

        var path = Uri.TryCreate(pathOrUrl, UriKind.Absolute, out var absolute) && !absolute.IsFile
            ? absolute.AbsolutePath
            : pathOrUrl.Split('?', '#')[0];
        return new PathString("/" + path.Trim().TrimStart('/').TrimEnd('/'));
    }
}
