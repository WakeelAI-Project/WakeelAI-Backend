using System;
using System.Text.Json.Serialization;

namespace Wakeel.Application.DTOs.Employees;

public record EmployeeDetailResponse
{
    [JsonPropertyName("record_id")] public Guid RecordId { get; init; }
    [JsonPropertyName("user_id")] public Guid UserId { get; init; }
    [JsonPropertyName("full_name")] public string FullName { get; init; } = string.Empty;
    [JsonPropertyName("email")] public string Email { get; init; } = string.Empty;
    [JsonPropertyName("job_title")] public string JobTitle { get; init; } = string.Empty;
    [JsonPropertyName("department_id")] public Guid DepartmentId { get; init; }
    [JsonPropertyName("department")] public string? Department { get; init; }
    [JsonPropertyName("national_id")] public string? NationalId { get; init; }
    [JsonPropertyName("hire_date")] public DateOnly HireDate { get; init; }
    [JsonPropertyName("salary")] public decimal Salary { get; init; }
    [JsonPropertyName("contract_type")] public string ContractType { get; init; } = string.Empty;
    [JsonPropertyName("employment_status")] public string EmploymentStatus { get; init; } = string.Empty;
    [JsonPropertyName("leave_balance")] public LeaveBalanceSummary? LeaveBalance { get; init; }
    [JsonPropertyName("current_leave")] public CurrentLeaveInfo? CurrentLeave { get; init; }
}
