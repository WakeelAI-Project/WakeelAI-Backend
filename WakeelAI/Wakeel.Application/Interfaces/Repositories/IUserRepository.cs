using System.Threading;
using System.Threading.Tasks;
using Wakeel.Domain.Entities;

namespace Wakeel.Application.Interfaces.Repositories;

/// <summary>
/// Defines data access operations specific to the <see cref="User"/> entity.
/// </summary>
public interface IUserRepository : IGenericRepository<User>
{
    /// <summary>
    /// Determines whether a user with the given email address already exists.
    /// </summary>
    /// <param name="email">The email address to check.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>True if a user with the given email exists; otherwise, false.</returns>
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

}