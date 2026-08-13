using System;
using System.Text.Json.Serialization;

namespace Wakeel.Application.DTOs.AiIntegrations;

public class EmployeeContextResponse
{
    [JsonPropertyName("record_id")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("company_id")]
    public string CompanyId { get; set; } = string.Empty;

    [JsonPropertyName("full_name")]
    public string FullName { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("department")]
    public string? Department { get; set; }

    [JsonPropertyName("job_title")]
    public string? JobTitle { get; set; }

    [JsonPropertyName("employment_status")]
    public string? EmploymentStatus { get; set; }

    [JsonPropertyName("leave_balance")]
    public EmployeeLeaveBalancesDto? LeaveBalance { get; set; }
}

public class EmployeeLeaveBalancesDto
{
    [JsonPropertyName("annual")]
    public LeaveBalanceContextDto? Annual { get; set; }

    [JsonPropertyName("sick")]
    public LeaveBalanceContextDto? Sick { get; set; }

    [JsonPropertyName("unpaid")]
    public LeaveBalanceContextDto? Unpaid { get; set; }
}

public class LeaveBalanceContextDto
{
    [JsonPropertyName("total_days")]
    public int TotalDays { get; set; }

    [JsonPropertyName("used_days")]
    public int UsedDays { get; set; }

    [JsonPropertyName("remaining_days")]
    public int RemainingDays { get; set; }
}
