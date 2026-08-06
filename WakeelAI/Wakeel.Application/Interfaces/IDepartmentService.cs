using System;
using System.Threading;
using System.Threading.Tasks;
using Wakeel.Application.DTOs.Departments;

namespace Wakeel.Application.Interfaces;

/// <summary>
/// Service interface for department management operations.
/// Defines CRUD and list operations for departments within a company.
/// </summary>
public interface IDepartmentService
{
    /// <summary>
    /// Creates a new department for a company.
    /// </summary>
    /// <param name="companyId">The company ID that owns the new department.</param>
    /// <param name="request">The department creation request.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The created department response.</returns>
    Task<DepartmentResponse> CreateAsync(Guid companyId, CreateDepartmentRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all departments for a company with pagination.
    /// </summary>
    /// <param name="companyId">The company ID to list departments for.</param>
    /// <param name="page">The page number (1-based).</param>
    /// <param name="limit">The number of items per page (1-100).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A paginated list of departments.</returns>
    Task<DepartmentListResponse> ListAsync(Guid companyId, int page, int limit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a single department by ID.
    /// </summary>
    /// <param name="companyId">The company ID that owns the department.</param>
    /// <param name="departmentId">The department ID to retrieve.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The department response, or null if not found or deleted.</returns>
    Task<DepartmentResponse?> GetAsync(Guid companyId, Guid departmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing department.
    /// </summary>
    /// <param name="companyId">The company ID that owns the department.</param>
    /// <param name="departmentId">The department ID to update.</param>
    /// <param name="request">The department update request.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The updated department response, or null if not found or deleted.</returns>
    Task<DepartmentResponse?> UpdateAsync(Guid companyId, Guid departmentId, UpdateDepartmentRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes a department.
    /// </summary>
    /// <param name="companyId">The company ID that owns the department.</param>
    /// <param name="departmentId">The department ID to delete.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A tuple containing:
    /// - Success: Whether the deletion succeeded.
    /// - ErrorCode: An error code if deletion failed (e.g., "department_in_use", "department_not_found"), null if successful.
    /// </returns>
    Task<(bool Success, string? ErrorCode)> DeleteAsync(Guid companyId, Guid departmentId, CancellationToken cancellationToken = default);
}
