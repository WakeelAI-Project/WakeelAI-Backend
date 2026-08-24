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

    [JsonPropertyName("salary")]
    public decimal? Salary { get; set; }

    [JsonPropertyName("hire_date")]
    public string? HireDate { get; set; }

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
    /// <summary>Null means uncapped (Sick, Unpaid) - never coerced to 0.</summary>
    [JsonPropertyName("total_days")]
    public int? TotalDays { get; set; }

    [JsonPropertyName("used_days")]
    public int UsedDays { get; set; }

    /// <summary>Null means uncapped (Sick, Unpaid) - never coerced to 0.</summary>
    [JsonPropertyName("remaining_days")]
    public int? RemainingDays { get; set; }

    /// <summary>Days held by the employee's own Pending requests of this type this year -
    /// already netted out of <see cref="RemainingDays"/>, surfaced so the assistant can
    /// explain why remaining is lower than total minus used (e.g. a request still awaiting
    /// HR review) instead of the employee being confused when a new request is rejected.</summary>
    [JsonPropertyName("reserved_days")]
    public int ReservedDays { get; set; }

    /// <summary>True when this leave type carries no day cap (Sick, Unpaid), so the
    /// assistant can describe it as "no cap" instead of inferring meaning from null.</summary>
    [JsonPropertyName("is_uncapped")]
    public bool IsUncapped { get; set; }
}
