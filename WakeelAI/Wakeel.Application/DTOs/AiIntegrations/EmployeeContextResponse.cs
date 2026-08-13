using System;
using System.Text.Json.Serialization;

namespace Wakeel.Application.DTOs.AiIntegrations;

public class EmployeeContextResponse
{
    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("companyId")]
    public string CompanyId { get; set; } = string.Empty;

    [JsonPropertyName("fullName")]
    public string FullName { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("department")]
    public string? Department { get; set; }

    [JsonPropertyName("employmentStatus")]
    public string? EmploymentStatus { get; set; }

    [JsonPropertyName("leaveBalance")]
    public LeaveBalanceContextDto? LeaveBalance { get; set; }
}

public class LeaveBalanceContextDto
{
    [JsonPropertyName("annual")]
    public int Annual { get; set; }

    [JsonPropertyName("used")]
    public int Used { get; set; }

    [JsonPropertyName("remaining")]
    public int Remaining { get; set; }
}
