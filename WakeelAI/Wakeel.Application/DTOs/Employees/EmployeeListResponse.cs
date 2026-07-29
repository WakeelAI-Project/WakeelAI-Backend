using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Wakeel.Application.DTOs.Employees;

public record EmployeeListResponse
{
    [JsonPropertyName("data")] public IEnumerable<EmployeeListItem> Data { get; init; } = new List<EmployeeListItem>();
    [JsonPropertyName("page")] public int Page { get; init; }
    [JsonPropertyName("total")] public int Total { get; init; }
}
