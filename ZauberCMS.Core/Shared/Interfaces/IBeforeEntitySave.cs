using Microsoft.EntityFrameworkCore;

namespace ZauberCMS.Core.Shared.Interfaces;

/// <summary>
/// Defines a contract for handling operations that need to occur before saving an entity to the database.
/// </summary>
// ReSharper disable once TypeParameterCanBeVariant
public interface IBeforeEntitySave<TEntity>
{
    /// <summary>
    /// Executes operations needed before saving an entity to the database.
    /// </summary>
    /// <param name="entity">The entity instance that is about to be saved.</param>
    /// <param name="entityState">The state of the entity within the context (e.g., Added, Modified, Deleted).</param>
    /// <returns>Returns false if the save operation should be canceled; otherwise, true.</returns>
    bool BeforeSave(TEntity entity, EntityState entityState);

    /// <summary>
    /// Gets or sets the order in which operations or processes should be executed or entities should be managed.
    /// </summary>
    /// <remarks>
    /// This property is typically used to indicate the priority or position of an entity or operation
    /// in contexts such as sorting, processing, or UI display.
    /// </remarks>
    int SortOrder { get; }
}