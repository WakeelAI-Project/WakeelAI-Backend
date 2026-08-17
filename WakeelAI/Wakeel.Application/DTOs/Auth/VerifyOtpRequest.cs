using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Wakeel.Application.DTOs.Auth;

public record VerifyOtpRequest
{
    [JsonPropertyName("email")]
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Email must be a valid email address.")]
    public string Email { get; init; } = string.Empty;

    [JsonPropertyName("otp")]
    [Required(ErrorMessage = "OTP is required.")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "OTP must be a 6-digit code.")]
    public string Otp { get; init; } = string.Empty;
}
