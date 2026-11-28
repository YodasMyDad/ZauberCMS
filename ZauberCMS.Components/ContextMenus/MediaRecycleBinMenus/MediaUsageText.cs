using Microsoft.Extensions.Localization;
using ZauberCMS.Core.Localization;
using ZauberCMS.Core.Media.Models;

namespace ZauberCMS.Components.ContextMenus.MediaRecycleBinMenus;

/// <summary>The sentence a permanent-delete confirmation uses to say where the media is still used.</summary>
internal static class MediaUsageText
{
    private const int NamesShown = 5;

    public static string Describe(IReadOnlyCollection<MediaUsage> usages, int max, IStringLocalizer<ZauberText> text)
    {
        if (usages.Count == 0)
        {
            return string.Empty;
        }

        var places = usages
            .Select(u => (u.Source, u.ItemId, Name: u.ItemInRecycleBin
                ? text["contextMenus_mediaUsageInRecycleBin", u.ItemName ?? text["contextMenus_mediaUsageNoName"].Value].Value
                : u.ItemName ?? text["contextMenus_mediaUsageNoName"].Value))
            .Distinct()
            .ToList();
        var pages = places.Count(p => p.Source == MediaUsageSource.Content);
        var members = places.Count(p => p.Source == MediaUsageSource.User);

        var counts = new List<string>();
        if (pages > 0) counts.Add(pages == 1 ? text["contextMenus_mediaUsagePagesOne"].Value : text["contextMenus_mediaUsagePages", pages].Value);
        if (members > 0) counts.Add(members == 1 ? text["contextMenus_mediaUsageMembersOne"].Value : text["contextMenus_mediaUsageMembers", members].Value);
        var usedBy = counts.Count == 2 ? text["contextMenus_mediaUsagePagesAndMembers", counts[0], counts[1]].Value : counts.FirstOrDefault() ?? string.Empty;

        var names = string.Join(", ", places.Take(NamesShown).Select(p => p.Name));
        if (places.Count > NamesShown) names = text["contextMenus_mediaUsageNamesAndMore", names, places.Count - NamesShown].Value;

        if (usages.Count >= max)
        {
            return text["contextMenus_mediaStillUsedByAtLeast", usedBy, names].Value;
        }

        return places.Count == 1
            ? text["contextMenus_mediaStillUsedByOne", usedBy, names].Value
            : text["contextMenus_mediaStillUsedBy", usedBy, names].Value;
    }
}
