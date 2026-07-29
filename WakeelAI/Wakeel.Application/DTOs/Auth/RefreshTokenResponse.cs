using System.Text.Json.Serialization;

namespace Wakeel.Application.DTOs.Auth;

public record RefreshTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; init; } = string.Empty;

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; init; }
}