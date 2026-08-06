using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wakeel.Application.DTOs.Departments;
using Wakeel.Application.Interfaces;
using Wakeel.Application.Interfaces.Repositories;
using Wakeel.Domain.Entities;

namespace Wakeel.Application.Services;

/// <summary>
/// Implementation of IDepartmentService.
/// Handles department CRUD operations with business logic validation.
/// </summary>
public class DepartmentService : IDepartmentService
{
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// Initializes a new instance of the DepartmentService class.
    /// </summary>
    /// <param name="unitOfWork">The unit of work for data access.</param>
    /// <exception cref="ArgumentNullException">Thrown if unitOfWork is null.</exception>
    public DepartmentService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    /// <summary>
    /// Creates a new department for the specified company.
    /// </summary>
    /// <param name="companyId">The company ID that owns the new department.</param>
    /// <param name="request">The department creation request.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The created department response.</returns>
    public async Task<DepartmentResponse> CreateAsync(Guid companyId, CreateDepartmentRequest request, CancellationToken cancellationToken = default)
    {
        var department = new Department
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Name = request.Name,
            Description = request.Description,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Departments.AddAsync(department, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToResponse(department);
    }

    /// <summary>
    /// Lists all departments for a company with pagination.
    /// Results are sorted alphabetically by department name.
    /// Excludes soft-deleted departments.
    /// </summary>
    /// <param name="companyId">The company ID to list departments for.</param>
    /// <param name="page">The page number (1-based).</param>
    /// <param name="limit">The number of items per page (1-100).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A paginated list of departments.</returns>
    public async Task<DepartmentListResponse> ListAsync(Guid companyId, int page, int limit, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        limit = Math.Clamp(limit, 1, 100);

        var all = await _unitOfWork.Departments.FindAsync(
            d => d.CompanyId == companyId && !d.IsDeleted,
            cancellationToken
        );

        var ordered = all.OrderBy(d => d.Name).ToList();
        var total = ordered.Count;
        var items = ordered
            .Skip((page - 1) * limit)
            .Take(limit)
            .Select(ToResponse)
            .ToList();

        return new DepartmentListResponse
        {
            Data = items,
            Page = page,
            Total = total
        };
    }

    /// <summary>
    /// Gets a single department by ID.
    /// Returns null if department is not found, belongs to a different company, or is deleted.
    /// </summary>
    /// <param name="companyId">The company ID that should own the department.</param>
    /// <param name="departmentId">The department ID to retrieve.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The department response, or null if not found or deleted.</returns>
    public async Task<DepartmentResponse?> GetAsync(Guid companyId, Guid departmentId, CancellationToken cancellationToken = default)
    {
        var department = await _unitOfWork.Departments.GetByIdAsync(departmentId, cancellationToken);

        if (department is null || department.CompanyId != companyId || department.IsDeleted)
            return null;

        return ToResponse(department);
    }

    /// <summary>
    /// Updates an existing department with new values.
    /// Only updates fields that are provided in the request.
    /// Returns null if department is not found, belongs to a different company, or is deleted.
    /// </summary>
    /// <param name="companyId">The company ID that should own the department.</param>
    /// <param name="departmentId">The department ID to update.</param>
    /// <param name="request">The department update request.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The updated department response, or null if not found or deleted.</returns>
    public async Task<DepartmentResponse?> UpdateAsync(Guid companyId, Guid departmentId, UpdateDepartmentRequest request, CancellationToken cancellationToken = default)
    {
        var department = await _unitOfWork.Departments.GetByIdAsync(departmentId, cancellationToken);

        if (department is null || department.CompanyId != companyId || department.IsDeleted)
            return null;

        if (!string.IsNullOrWhiteSpace(request.Name))
            department.Name = request.Name!;

        if (request.Description is not null)
            department.Description = request.Description;

        _unitOfWork.Departments.Update(department);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToResponse(department);
    }

    /// <summary>
    /// Soft-deletes a department by setting IsDeleted = true.
    /// Prevents deletion if department has assigned employees.
    /// </summary>
    /// <param name="companyId">The company ID that should own the department.</param>
    /// <param name="departmentId">The department ID to delete.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A tuple containing:
    /// - Success: Whether the deletion succeeded.
    /// - ErrorCode: An error code if deletion failed, null if successful.
    /// </returns>
    public async Task<(bool Success, string? ErrorCode)> DeleteAsync(Guid companyId, Guid departmentId, CancellationToken cancellationToken = default)
    {
        var department = await _unitOfWork.Departments.GetByIdAsync(departmentId, cancellationToken);

        if (department is null || department.CompanyId != companyId || department.IsDeleted)
            return (false, "department_not_found");

        // Check if any employees are assigned to this department
        var isInUse = await _unitOfWork.EmployeeProfiles.AnyAsync(
            e => e.DepartmentId == departmentId,
            cancellationToken
        );

        if (isInUse)
            return (false, "department_in_use");

        // Soft delete: mark as deleted instead of removing from DB
        department.IsDeleted = true;
        _unitOfWork.Departments.Update(department);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (true, null);
    }

    /// <summary>
    /// Maps a Department entity to a DepartmentResponse DTO.
    /// </summary>
    /// <param name="department">The department entity to map.</param>
    /// <returns>The mapped department response.</returns>
    private static DepartmentResponse ToResponse(Department department)
    {
        return new DepartmentResponse
        {
            Id = department.Id,
            Name = department.Name,
            Description = department.Description,
            CreatedAt = department.CreatedAt
        };
    }
}
