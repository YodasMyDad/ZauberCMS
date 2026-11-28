using System.Globalization;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Data.Interfaces;
using ZauberCMS.Core.Data.Models;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Models;

namespace ZauberCMS.Core.SeedData;

/// <summary>
/// Creates the back-office role "Editor", with the Content and Media sections (Roadmap D38, D69), once per database.
/// <list type="bullet">
/// <item>Once: a <c>GlobalData</c> row (<see cref="MarkerAlias"/>) records that it ran, so a site that deletes or
/// renames the role does not get it back at the next start.</item>
/// <item>Only when no role of that name exists: an "Editor" role a site already has (it may be a role for front-end
/// members) is left exactly as it is, so nobody gets into the admin by the upgrade.</item>
/// </list>
/// </summary>
public class SeedEditorRole : ISeedData
{
    /// <summary>The <c>GlobalData.Alias</c> that records the role was seeded.</summary>
    public const string MarkerAlias = "ZauberEditorRoleSeeded";

    public void Initialise(IZauberDbContext dbContext)
    {
        if (dbContext.GlobalDatas.Any(x => x.Alias == MarkerAlias))
        {
            return;
        }

        var normalisedName = Constants.Roles.EditorRoleName.ToUpperInvariant();
        if (!dbContext.Roles.Any(x => x.NormalizedName == normalisedName))
        {
            var editor = new Role
            {
                Id = Guid.NewGuid().NewSequentialGuid(),
                Name = Constants.Roles.EditorRoleName,
                NormalizedName = normalisedName,
                ConcurrencyStamp = Guid.NewGuid().ToString(),
                Description = "Edits the site's content and media.",
                Icon = "edit_note"
            };
            editor.SetAllowedSections([Constants.Sections.ContentSection, Constants.Sections.MediaSection]);
            dbContext.Roles.Add(editor);
        }

        dbContext.GlobalDatas.Add(new GlobalData { Alias = MarkerAlias, Data = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) });
        dbContext.SaveChanges();
    }
}
