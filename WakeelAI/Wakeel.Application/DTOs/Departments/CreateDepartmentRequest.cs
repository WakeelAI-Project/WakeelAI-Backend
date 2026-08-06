using System.ComponentModel.DataAnnotations;

namespace Wakeel.Application.DTOs.Departments;

/// <summary>
/// Request DTO for creating a new department.
/// </summary>
public record CreateDepartmentRequest
{
    /// <summary>
    /// The name of the department. Required, max 200 characters.
    /// </summary>
    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(200)]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Optional description of the department. Max 500 characters.
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; init; }
}
