using System;
using System.Threading;
using System.Threading.Tasks;
using Wakeel.Domain.Entities;

namespace Wakeel.Application.Interfaces;

public interface IResourceLoader
{
    Task<EmployeeProfile?> GetEmployeeProfileAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<LeaveRequest?> GetLeaveRequestAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Department?> GetDepartmentAsync(Guid id, CancellationToken cancellationToken = default);
    Task<DocumentTemplate?> GetDocumentTemplateAsync(Guid id, CancellationToken cancellationToken = default);
    Task<GeneratedDocument?> GetGeneratedDocumentAsync(Guid id, CancellationToken cancellationToken = default);
    Task<User?> GetUserAsync(Guid id, CancellationToken cancellationToken = default);
}
