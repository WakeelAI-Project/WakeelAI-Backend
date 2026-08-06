using System;

namespace Wakeel.Application.DTOs.Departments;

/// <summary>
/// Response DTO for a single department.
/// Returned by Get, Create, and Update operations.
/// </summary>
public record DepartmentResponse
{
    /// <summary>
    /// Unique identifier of the department.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// The name of the department.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Optional description of the department.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// The date and time when the department was created.
    /// </summary>
    public DateTime CreatedAt { get; init; }
}
