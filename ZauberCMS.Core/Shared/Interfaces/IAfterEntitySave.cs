using Microsoft.EntityFrameworkCore;

namespace ZauberCMS.Core.Shared.Interfaces;

/// <summary>
/// Defines a contract for handling operations that need to occur after saving an entity to the database.
/// </summary>
// ReSharper disable once TypeParameterCanBeVariant
public interface IAfterEntitySave<TEntity>
{
    /// <summary>
    /// Executes operations needed after saving an entity to the database.
    /// </summary>
    /// <param name="entity">The entity instance that is about to be saved.</param>
    /// <param name="entityState">The state of the entity within the context (e.g., Added, Modified, Deleted).</param>
    /// <returns>True if the entity needs to be resaved</returns>
    bool AfterSave(TEntity entity, EntityState entityState);

    /// <summary>
    /// Gets or sets the order in which the operation should be executed after saving an entity.
    /// </summary>
    int SortOrder { get; }
}