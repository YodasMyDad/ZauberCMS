using Microsoft.EntityFrameworkCore;

namespace ZauberCMS.Core.Shared.Interfaces;

/// <summary>
/// Runs once a change is stored for good: after the commit of the transaction the save ran in, or right after the
/// save when it ran without one (Roadmap D62). A change that is rolled back, vetoed or fails never reaches it, so
/// this is the place for work outside the database that must only follow a real change: mirroring files, telling
/// another system, indexing, sending mail. Discovered like the other save hooks, for every row the change wrote, of
/// every entity type, each with the state it was saved in. It cannot stop or change the change; an exception it
/// throws is logged and the other hooks still run.
/// </summary>
/// <remarks>
/// <see cref="IAfterEntitySave{TEntity}"/> runs earlier, inside the transaction and before the commit, and can ask
/// for a second save; use it only for changes that must be written with the change itself.
/// </remarks>
// ReSharper disable once TypeParameterCanBeVariant
public interface IAfterEntityCommit<TEntity>
{
    /// <summary>Runs after the change that wrote <paramref name="entity"/> is committed.</summary>
    /// <param name="entity">The row as it was saved (a deleted row is no longer tracked).</param>
    /// <param name="entityState">The state it was saved in: Added, Modified or Deleted (Unchanged for the item
    /// the save named when it had no changes).</param>
    /// <param name="cancellationToken">The caller's token. The change is stored whatever happens to it.</param>
    Task AfterCommitAsync(TEntity entity, EntityState entityState, CancellationToken cancellationToken = default);

    /// <summary>The order in which the hooks for one entity type run, lowest first.</summary>
    int SortOrder { get; }
}
