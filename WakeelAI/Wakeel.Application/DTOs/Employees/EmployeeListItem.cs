using System;
using System.Text.Json.Serialization;

namespace Wakeel.Application.DTOs.Employees;

public record EmployeeListItem
{
    [JsonPropertyName("record_id")] public Guid RecordId { get; init; }
    [JsonPropertyName("user_id")] public Guid UserId { get; init; }
    [JsonPropertyName("full_name")] public string FullName { get; init; } = string.Empty;
    [JsonPropertyName("job_title")] public string JobTitle { get; init; } = string.Empty;
    [JsonPropertyName("employment_status")] public string EmploymentStatus { get; init; } = string.Empty;
}
