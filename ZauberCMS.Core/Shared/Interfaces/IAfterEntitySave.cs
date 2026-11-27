using Microsoft.EntityFrameworkCore;

namespace ZauberCMS.Core.Shared.Interfaces;

/// <summary>
/// Runs after a change's rows are written, inside its transaction and before the commit, and can ask for a second
/// save of changes it makes (Roadmap D28). The change can still be rolled back after it (a media file that can't be
/// removed, a later step of the caller that fails), so work outside the database that must only follow a stored
/// change belongs in <see cref="IAfterEntityCommit{TEntity}"/> (D62).
/// </summary>
// ReSharper disable once TypeParameterCanBeVariant
public interface IAfterEntitySave<TEntity>
{
    /// <summary>
    /// Executes operations needed after saving an entity to the database.
    /// </summary>
    /// <param name="entity">The entity instance that was written.</param>
    /// <param name="entityState">The state of the entity within the context (e.g., Added, Modified, Deleted).</param>
    /// <returns>True if the entity needs to be resaved</returns>
    bool AfterSave(TEntity entity, EntityState entityState);

    /// <summary>
    /// Gets or sets the order in which the operation should be executed after saving an entity.
    /// </summary>
    int SortOrder { get; }
}