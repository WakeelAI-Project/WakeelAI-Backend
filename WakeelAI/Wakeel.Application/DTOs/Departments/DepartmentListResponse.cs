using System.Collections.Generic;

namespace Wakeel.Application.DTOs.Departments;

/// <summary>
/// Response DTO for listing departments with pagination metadata.
/// Returned by the List operation.
/// </summary>
public record DepartmentListResponse
{
    /// <summary>
    /// List of departments on the current page.
    /// </summary>
    public List<DepartmentResponse> Data { get; init; } = new();

    /// <summary>
    /// Current page number.
    /// </summary>
    public int Page { get; init; }

    /// <summary>
    /// Total number of departments (not just on this page).
    /// </summary>
    public int Total { get; init; }
}
