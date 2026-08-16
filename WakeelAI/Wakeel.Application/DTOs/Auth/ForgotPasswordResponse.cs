using System.Text.Json.Serialization;

namespace Wakeel.Application.DTOs.Auth;

public record ForgotPasswordResponse
{
    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;
}
