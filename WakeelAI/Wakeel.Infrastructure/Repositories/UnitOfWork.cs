using System;
using System.Threading;
using System.Threading.Tasks;
using Wakeel.Application.Interfaces.Repositories;
using Wakeel.Infrastructure.Persistence;

namespace Wakeel.Infrastructure.Repositories;

/// <summary>
/// Coordinates repository operations against a single <see cref="ApplicationDbContext"/> instance
/// and commits them as one atomic transaction.
/// Repository instances are created lazily and cached for the lifetime of this unit of work.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _dbContext;
    private IUserRepository? _users;
    private ICompanyRepository? _companies;
    private bool _disposed;

    public UnitOfWork(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <inheritdoc />
    public IUserRepository Users => _users ??= new UserRepository(_dbContext);

    /// <inheritdoc />
    public ICompanyRepository Companies => _companies ??= new CompanyRepository(_dbContext);

    /// <inheritdoc />
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            _dbContext.Dispose();
        }

        _disposed = true;
    }
}