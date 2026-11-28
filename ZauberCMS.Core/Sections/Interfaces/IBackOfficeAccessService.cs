using System.Security.Claims;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Sections.Models;

namespace ZauberCMS.Core.Sections.Interfaces;

/// <summary>
/// What a signed-in user may do in the admin (Roadmap D38, D66–D68): which sections they may open, and, for someone
/// who manages users and roles, which changes they may make without giving anybody (themselves included) more than
/// they have. Administrators may do everything.
/// <para>
/// This is the check the admin's pages, its navigation, <c>AdminAccessMiddleware</c> and the section policies
/// (<c>AuthorizeSectionAttribute</c>) ask. The services themselves still do no authorisation (D27): a page, component
/// or endpoint asks here before it calls them. Replace it (register your own implementation after
/// <c>AddZauberCms</c>) to change the rules everywhere at once.
/// </para>
/// </summary>
public interface IBackOfficeAccessService
{
    /// <summary>
    /// Every discovered section (<see cref="ISection"/>), in <see cref="ISection.SortOrder"/>; ZauberCMS's and the
    /// plugins' alike.
    /// </summary>
    IReadOnlyList<ISection> GetAllSections();

    /// <summary>
    /// The sections <paramref name="user"/> may open, in <see cref="ISection.SortOrder"/>: all of them for an
    /// administrator, the sections of the user's roles put together for anyone else (the starting role adds none,
    /// D71), none for nobody signed in.
    /// </summary>
    Task<IReadOnlyList<ISection>> GetSectionsAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default);

    /// <summary>Whether <paramref name="user"/> may open the section <paramref name="sectionAlias"/>.</summary>
    Task<bool> CanOpenSectionAsync(ClaimsPrincipal user, string sectionAlias, CancellationToken cancellationToken = default);

    /// <summary>
    /// The section whose pages an admin address belongs to: the discovered section with the longest
    /// <see cref="ISection.IndexUrl"/> the path is at or below, on whole segments (<c>/admin/forms/edit/1</c> →
    /// the section at <c>/admin/forms</c>). Null for <c>/admin</c> itself and for an address under no section. The
    /// path is relative to the site's path base.
    /// </summary>
    ISection? GetSectionForPath(string? path);

    /// <summary>
    /// Whether <paramref name="editor"/> may give the role <paramref name="roleName"/> to a user: an administrator
    /// any role; anyone else only a role they are in, never the Admin role. From the roles the editor's sign-in
    /// carries, for showing what can be ticked; the save asks <see cref="CanSetUserRolesAsync"/>, which reads the roles
    /// as stored.
    /// </summary>
    bool CanGiveRole(ClaimsPrincipal editor, string? roleName);

    /// <summary>
    /// Whether <paramref name="editor"/> may change the user <paramref name="userId"/> (details, password, roles,
    /// delete): an administrator anyone; someone who may open the Users section anyone who is not an administrator and
    /// opens no section the editor cannot open (setting their password would otherwise hand the editor those sections).
    /// This and the other checks that could give access read the editor's roles as stored, not their sign-in's.
    /// </summary>
    Task<BackOfficeAccessResult> CanEditUserAsync(ClaimsPrincipal editor, Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether <paramref name="editor"/> may set the roles of the user <paramref name="userId"/> to
    /// <paramref name="roleNames"/>: <see cref="CanEditUserAsync"/>, and every role added is one
    /// <see cref="CanGiveRole"/> allows. Taking a role away is allowed.
    /// </summary>
    Task<BackOfficeAccessResult> CanSetUserRolesAsync(ClaimsPrincipal editor, Guid userId,
        IEnumerable<string> roleNames, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether <paramref name="editor"/> may open the role <paramref name="roleId"/> to change it: an administrator any
    /// role; someone who may open the Users section a role they are in, never the Admin role. A new role (an id not
    /// stored yet) is allowed.
    /// </summary>
    Task<BackOfficeAccessResult> CanEditRoleAsync(ClaimsPrincipal editor, Guid roleId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether <paramref name="editor"/> may save <paramref name="role"/> as it is now: <see cref="CanEditRoleAsync"/>,
    /// a name that is not the Admin role's, every section added to it one the editor may open, and no section on the
    /// starting role (<c>Zauber:NewUserStartingRole</c>, which every registration gets; Roadmap D71).
    /// </summary>
    Task<BackOfficeAccessResult> CanSaveRoleAsync(ClaimsPrincipal editor, Role role,
        CancellationToken cancellationToken = default);

    /// <summary>Whether <paramref name="editor"/> may delete the role <paramref name="roleId"/>: as <see cref="CanEditRoleAsync"/>.</summary>
    Task<BackOfficeAccessResult> CanDeleteRoleAsync(ClaimsPrincipal editor, Guid roleId,
        CancellationToken cancellationToken = default);
}
