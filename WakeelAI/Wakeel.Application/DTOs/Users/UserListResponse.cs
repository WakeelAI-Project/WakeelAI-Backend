using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Wakeel.Application.DTOs.Users;

public record UserListResponse
{
    [JsonPropertyName("data")] public IEnumerable<UserListItem> Data { get; init; } = new List<UserListItem>();
    [JsonPropertyName("page")] public int Page { get; init; }
    [JsonPropertyName("total")] public int Total { get; init; }
}
