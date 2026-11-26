using ZauberCMS.Core.Media.Models;

namespace ZauberCMS.Components.ContextMenus.MediaRecycleBinMenus;

/// <summary>The sentence a permanent-delete confirmation uses to say where the media is still used.</summary>
internal static class MediaUsageText
{
    private const int NamesShown = 5;

    public static string Describe(IReadOnlyCollection<MediaUsage> usages, int max)
    {
        if (usages.Count == 0)
        {
            return string.Empty;
        }

        var places = usages
            .Select(u => (u.Source, u.ItemId, Name: (u.ItemName ?? "(no name)") + (u.ItemInRecycleBin ? " (in the recycle bin)" : string.Empty)))
            .Distinct()
            .ToList();
        var pages = places.Count(p => p.Source == MediaUsageSource.Content);
        var members = places.Count(p => p.Source == MediaUsageSource.User);

        var counts = new List<string>();
        if (pages > 0) counts.Add(pages == 1 ? "1 page or block" : $"{pages} pages or blocks");
        if (members > 0) counts.Add(members == 1 ? "1 member" : $"{members} members");

        var names = string.Join(", ", places.Take(NamesShown).Select(p => p.Name));
        var more = places.Count > NamesShown ? $" and {places.Count - NamesShown} more" : string.Empty;
        var atLeast = usages.Count >= max ? "at least " : string.Empty;

        var they = places.Count == 1 && atLeast.Length == 0 ? "It" : "They";
        return $"Still used by {atLeast}{string.Join(" and ", counts)}: {names}{more}. {they} will show a missing file. ";
    }
}
