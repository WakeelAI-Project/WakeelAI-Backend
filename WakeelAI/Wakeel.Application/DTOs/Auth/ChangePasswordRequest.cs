using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Wakeel.Application.DTOs.Auth;

public record ChangePasswordRequest
{
    [JsonPropertyName("current_password")]
    [Required]
    public string CurrentPassword { get; init; } = string.Empty;

    [JsonPropertyName("new_password")]
    [Required]
    [MinLength(8)]
    public string NewPassword { get; init; } = string.Empty;
}
