namespace ZauberCMS.Core.Media.Models;

/// <summary>
/// One place that points at a media item: a property value of a page (or block) or of a member.
/// </summary>
/// <param name="MediaId">The media item that is used.</param>
/// <param name="Source">Where the reference is.</param>
/// <param name="ItemId">The id of the content item or user.</param>
/// <param name="ItemName">Its name, for showing to an editor.</param>
/// <param name="ItemInRecycleBin">The content item or user is itself in the recycle bin.</param>
public sealed record MediaUsage(Guid MediaId, MediaUsageSource Source, Guid ItemId, string? ItemName, bool ItemInRecycleBin);

public enum MediaUsageSource
{
    Content,
    User
}
