using System;
using System.Threading;
using System.Threading.Tasks;
using Wakeel.Application.DTOs.Employees;

namespace Wakeel.Application.Interfaces;

public interface IEmployeeService
{
    Task<CreateEmployeeResponse> CreateEmployeeAsync(Guid actorUserId, Guid companyId, CreateEmployeeRequest request, CancellationToken cancellationToken = default);
    Task<EmployeeDetailResponse?> GetEmployeeAsync(Guid companyId, Guid recordId, CancellationToken cancellationToken = default);
    Task<EmployeeListResponse> ListEmployeesAsync(Guid companyId, string? status, string? search, int page, int limit, CancellationToken cancellationToken = default);
    Task<EmployeeDetailResponse?> UpdateEmployeeAsync(Guid companyId, Guid recordId, UpdateEmployeeRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeactivateEmployeeAsync(Guid companyId, Guid recordId, CancellationToken cancellationToken = default);
}
