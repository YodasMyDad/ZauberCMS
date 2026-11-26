namespace ZauberCMS.Core.Media.Models;

/// <summary>
/// Per-URL access descriptor for a restricted media item.
/// An empty <see cref="AllowedRoleNames"/> list means any authenticated user is allowed.
/// </summary>
/// <param name="InRecycleBin">Every media item with this file is in the recycle bin: only administrators get it,
/// everyone else gets 404.</param>
public sealed record RestrictedMediaEntry(Guid MediaId, IReadOnlyList<string> AllowedRoleNames, bool InRecycleBin = false);
