using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Wakeel.Application.DTOs.Users;

public record InviteUserRequest
{
    [JsonPropertyName("full_name")]
    [Required]
    public string FullName { get; init; } = string.Empty;

    [JsonPropertyName("email")]
    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;

    [JsonPropertyName("role")]
    [Required]
    public string Role { get; init; } = string.Empty; // Expected: HR_Manager | Employee
}
