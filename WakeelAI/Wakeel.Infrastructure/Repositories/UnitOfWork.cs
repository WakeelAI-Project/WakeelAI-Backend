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
    private IRefreshTokenRepository? _refreshTokens;
    private IEmployeeProfileRepository? _employeeProfiles;
    private IDepartmentRepository? _departments;
    private ILeaveBalanceRepository? _leaveBalances;
    private ILeaveRequestRepository? _leaveRequests;
    private IDocumentTemplateRepository? _documentTemplates;
    private IGeneratedDocumentRepository? _generatedDocuments;
    private IAuditLogRepository? _auditLogs;
    private IPasswordResetOtpRepository? _passwordResetOtps;
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
    public IRefreshTokenRepository RefreshTokens => _refreshTokens ??= new RefreshTokenRepository(_dbContext);

    /// <summary>
    /// Repository for employee profile records.
    /// </summary>
    public IEmployeeProfileRepository EmployeeProfiles => _employeeProfiles ??= new EmployeeProfileRepository(_dbContext);

    /// <summary>
    /// Repository for department records.
    /// </summary>
    public IDepartmentRepository Departments => _departments ??= new DepartmentRepository(_dbContext);

    /// <summary>
    /// Repository for leave balance records.
    /// </summary>
    public ILeaveBalanceRepository LeaveBalances => _leaveBalances ??= new LeaveBalanceRepository(_dbContext);

    /// <summary>
    /// Repository for leave request records.
    /// </summary>
    public ILeaveRequestRepository LeaveRequests => _leaveRequests ??= new LeaveRequestRepository(_dbContext);

    public IDocumentTemplateRepository DocumentTemplates => _documentTemplates ??= new DocumentTemplateRepository(_dbContext);
    public IGeneratedDocumentRepository GeneratedDocuments => _generatedDocuments ??= new GeneratedDocumentRepository(_dbContext);
    public IAuditLogRepository AuditLogs => _auditLogs ??= new AuditLogRepository(_dbContext);

    /// <inheritdoc />
    public IPasswordResetOtpRepository PasswordResetOtps => _passwordResetOtps ??= new PasswordResetOtpRepository(_dbContext);

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