using System;
using System.Text.Json.Serialization;

namespace Wakeel.Application.DTOs.Employees;

public record UpdateEmployeeRequest
{
    [JsonPropertyName("job_title")] public string? JobTitle { get; init; }
    [JsonPropertyName("salary")] public decimal? Salary { get; init; }
    [JsonPropertyName("employment_status")] public string? EmploymentStatus { get; init; }
    [JsonPropertyName("contract_type")] public string? ContractType { get; init; }
}
