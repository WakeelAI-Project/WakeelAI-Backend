using System;
using System.Text.Json.Serialization;

namespace Wakeel.Application.DTOs.Users;

public record UserProfileResponse
{
    [JsonPropertyName("user_id")] public Guid UserId { get; init; }
    [JsonPropertyName("company_id")] public Guid CompanyId { get; init; }
    [JsonPropertyName("company_name")] public string CompanyName { get; init; } = string.Empty;
    [JsonPropertyName("full_name")] public string FullName { get; init; } = string.Empty;
    [JsonPropertyName("email")] public string Email { get; init; } = string.Empty;
    [JsonPropertyName("phone")] public string Phone { get; init; } = string.Empty;
    [JsonPropertyName("role")] public string Role { get; init; } = string.Empty;
    [JsonPropertyName("is_active")] public bool IsActive { get; init; }
    [JsonPropertyName("created_at")] public DateTime CreatedAt { get; init; }
}
