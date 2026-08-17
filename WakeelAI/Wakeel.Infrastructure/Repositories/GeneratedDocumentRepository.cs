using Wakeel.Application.Interfaces.Repositories;
using Wakeel.Domain.Entities;
using Wakeel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Wakeel.Infrastructure.Repositories;

public class GeneratedDocumentRepository : GenericRepository<GeneratedDocument>, IGeneratedDocumentRepository
{
    public GeneratedDocumentRepository(ApplicationDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<(System.Collections.Generic.IReadOnlyList<GeneratedDocument> Items, int Total)> GetPagedAsync(
        int page, int limit, string? type, string? status, System.Guid? employeeId,
        string? sort, string? order, System.Threading.CancellationToken cancellationToken = default)
    {
        System.Linq.IQueryable<GeneratedDocument> query = DbContext.GeneratedDocuments.AsNoTracking();

        if (!string.IsNullOrEmpty(type))
            query = query.Where(d => d.DocumentType == type);
        if (!string.IsNullOrEmpty(status))
            query = query.Where(d => d.Status == status);
        if (employeeId.HasValue)
            query = query.Where(d => d.EmployeeId == employeeId.Value);

        var isAsc = string.Equals(order, "asc", System.StringComparison.OrdinalIgnoreCase);
        query = sort?.ToLower() switch
        {
            "created_at" => isAsc ? query.OrderBy(d => d.CreatedAt) : query.OrderByDescending(d => d.CreatedAt),
            "updated_at" => isAsc ? query.OrderBy(d => d.UpdatedAt) : query.OrderByDescending(d => d.UpdatedAt),
            "title"      => isAsc ? query.OrderBy(d => d.Title)     : query.OrderByDescending(d => d.Title),
            _            => query.OrderByDescending(d => d.CreatedAt)
        };

        var total = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(query, cancellationToken);
        var items = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(query.Skip((page - 1) * limit).Take(limit), cancellationToken);
        return (items, total);
    }
}
