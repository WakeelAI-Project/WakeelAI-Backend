using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Wakeel.Application.Interfaces;
using Wakeel.Domain.Entities;
using Wakeel.Infrastructure.Persistence;

namespace Wakeel.Infrastructure.Services;

public class ResourceLoader : IResourceLoader
{
    private readonly ApplicationDbContext _dbContext;

    public ResourceLoader(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<EmployeeProfile?> GetEmployeeProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // EmployeeProfile primary key is UserId
        return await _dbContext.EmployeeProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(ep => ep.UserId == userId, cancellationToken);
    }

    public async Task<LeaveRequest?> GetLeaveRequestAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.LeaveRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(lr => lr.Id == id, cancellationToken);
    }

    public async Task<Department?> GetDepartmentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Departments
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }

    public async Task<DocumentTemplate?> GetDocumentTemplateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.DocumentTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<GeneratedDocument?> GetGeneratedDocumentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.GeneratedDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }

    public async Task<User?> GetUserAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }
}
