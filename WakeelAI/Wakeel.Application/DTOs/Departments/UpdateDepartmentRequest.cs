using System.ComponentModel.DataAnnotations;

namespace Wakeel.Application.DTOs.Departments;

/// <summary>
/// Request DTO for updating an existing department.
/// All fields are optional for partial updates.
/// </summary>
public record UpdateDepartmentRequest
{
    /// <summary>
    /// Optional new name for the department. Max 200 characters.
    /// </summary>
    [MaxLength(200)]
    public string? Name { get; init; }

    /// <summary>
    /// Optional new description for the department. Max 500 characters.
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; init; }
}
