using System.Text.Json.Serialization;

namespace Wakeel.Application.DTOs.Users;

public record InviteUserResponse
{
    [JsonPropertyName("user_id")] public Guid UserId { get; init; }
    [JsonPropertyName("email")] public string Email { get; init; } = string.Empty;
    [JsonPropertyName("role")] public string Role { get; init; } = string.Empty;
    [JsonPropertyName("status")] public string Status { get; init; } = "invited";
}
