using Microsoft.EntityFrameworkCore;

namespace ZauberCMS.Core.Shared.Interfaces;

/// <summary>
/// Defines a contract for handling operations that need to occur before saving an entity to the database.
/// </summary>
public interface IBeforeEntitySave<TEntity>
{
    /// <summary>
    /// Executes operations needed before saving an entity to the database.
    /// </summary>
    /// <param name="entity">The entity instance that is about to be saved.</param>
    /// <param name="entityState">The state of the entity within the context (e.g., Added, Modified, Deleted).</param>
    /// <returns>Returns false if the save operation should be canceled; otherwise, true.</returns>
    bool BeforeSave(TEntity entity, EntityState entityState);
}