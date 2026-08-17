using Wakeel.Domain.Entities;

namespace Wakeel.Application.Interfaces.Repositories;

public interface IGeneratedDocumentRepository : IGenericRepository<GeneratedDocument>
{
    Task<(System.Collections.Generic.IReadOnlyList<GeneratedDocument> Items, int Total)> GetPagedAsync(
        int page, int limit, string? type, string? status, System.Guid? employeeId,
        string? sort, string? order, System.Threading.CancellationToken cancellationToken = default);
}
