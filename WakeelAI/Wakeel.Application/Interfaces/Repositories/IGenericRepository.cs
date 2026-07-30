using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace Wakeel.Application.Interfaces.Repositories;

/// <summary>
/// Defines generic, entity-agnostic data access operations.
/// Implemented by the Infrastructure layer for any entity type.
/// </summary>
/// <typeparam name="T">The entity type this repository manages.</typeparam>
public interface IGenericRepository<T> where T : class
{
    /// <summary>
    /// Retrieves an entity by its primary key.
    /// </summary>
    /// <param name="id">The primary key of the entity.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The entity if found; otherwise, null.</returns>
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all entities of type <typeparamref name="T"/>.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all entities matching the given predicate.
    /// </summary>
    /// <param name="predicate">The filter expression to apply.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the first entity matching the given predicate, or null if none exists.
    /// </summary>
    /// <param name="predicate">The filter expression to apply.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether any entity matches the given predicate.
    /// </summary>
    /// <param name="predicate">The filter expression to apply.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new entity to the change tracker. Does not persist changes to the database;
    /// call <see cref="IUnitOfWork.SaveChangesAsync"/> to commit.
    /// </summary>
    /// <param name="entity">The entity to add.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task AddAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks an existing entity as modified. Does not persist changes to the database.
    /// </summary>
    /// <param name="entity">The entity to update.</param>
    void Update(T entity);

    /// <summary>
    /// Marks an entity for removal. Does not persist changes to the database.
    /// </summary>
    /// <param name="entity">The entity to remove.</param>
    void Remove(T entity);
}