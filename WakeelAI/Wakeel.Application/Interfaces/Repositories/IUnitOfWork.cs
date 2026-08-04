using System;
using System.Threading;
using System.Threading.Tasks;

namespace Wakeel.Application.Interfaces.Repositories;

/// <summary>
/// Coordinates repository operations against a single database context
/// and commits them as one atomic transaction.
/// </summary>
public interface IUnitOfWork : IDisposable
{
    /// <summary>
    /// Gets the repository for <see cref="Wakeel.Domain.Entities.User"/> entities.
    /// </summary>
    IUserRepository Users { get; }

    /// <summary>
    /// Gets the repository for <see cref="Wakeel.Domain.Entities.Company"/> entities.
    /// </summary>
    ICompanyRepository Companies { get; }

    /// <summary>
    /// Gets the repository for <see cref="Wakeel.Domain.Entities.RefreshToken"/> entities.
    /// </summary>
    IRefreshTokenRepository RefreshTokens { get; }
    /// <summary>
    /// Gets the repository for <see cref="Wakeel.Domain.Entities.EmployeeProfile"/> entities.
    /// </summary>
    IEmployeeProfileRepository EmployeeProfiles { get; }

    /// <summary>
    /// Gets the repository for <see cref="Wakeel.Domain.Entities.Department"/> entities.
    /// </summary>
    IDepartmentRepository Departments { get; }

    /// <summary>
    /// Gets the repository for <see cref="Wakeel.Domain.Entities.LeaveBalance"/> entities.
    /// </summary>
    ILeaveBalanceRepository LeaveBalances { get; }
    /// <summary>
    /// Persists all pending changes tracked by this unit of work to the database.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The number of state entries written to the database.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}