using System;

namespace Wakeel.Domain.Entities;

/// <summary>
/// Represents a department entity within a company.
/// Departments organize employees and define organizational structure.
/// </summary>
public class Department
{
    /// <summary>
    /// Unique identifier for the department.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The company that owns this department.
    /// </summary>
    public Guid CompanyId { get; set; }

    /// <summary>
    /// The name of the department (e.g., "Sales", "Engineering", "HR").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional description of the department's purpose or responsibilities.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Soft delete flag: indicates if the department is logically deleted.
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// The date and time when the department was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Navigation property: the company that owns this department.
    /// </summary>
    public Company Company { get; set; } = null!;
}
