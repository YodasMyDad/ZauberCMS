namespace ZauberCMS.Core.Media.Models;

/// <summary>
/// Per-URL access descriptor for a restricted media item.
/// An empty <see cref="AllowedRoleNames"/> list means any authenticated user is allowed.
/// </summary>
public sealed record RestrictedMediaEntry(Guid MediaId, IReadOnlyList<string> AllowedRoleNames);
