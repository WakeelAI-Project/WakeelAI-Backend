using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Wakeel.Application.DTOs.Auth;

public record LoginRequest
{
    [JsonPropertyName("email")]
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Email must be a valid email address.")]
    public string Email { get; init; } = string.Empty;

    [JsonPropertyName("password")]
    [Required(ErrorMessage = "Password is required.")]
    public string Password { get; init; } = string.Empty;
}