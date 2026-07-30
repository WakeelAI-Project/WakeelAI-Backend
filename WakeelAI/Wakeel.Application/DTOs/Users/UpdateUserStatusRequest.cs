using System.Text.Json.Serialization;

namespace Wakeel.Application.DTOs.Users;

public record UpdateUserStatusRequest
{
    [JsonPropertyName("is_active")] public bool IsActive { get; init; }
}
