using System.Text.Json.Serialization;

namespace Wakeel.Application.DTOs.Users;

public record UserListItem
{
    [JsonPropertyName("user_id")] public Guid UserId { get; init; }
    [JsonPropertyName("full_name")] public string FullName { get; init; } = string.Empty;
    [JsonPropertyName("role")] public string Role { get; init; } = string.Empty;
    [JsonPropertyName("is_active")] public bool IsActive { get; init; }
}
