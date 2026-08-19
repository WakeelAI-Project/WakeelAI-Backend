using System.Text.Json.Serialization;

namespace Wakeel.Application.DTOs.Employees;

/// <summary>
/// The employee's approved leave request whose date range includes today, if any.
/// Computed live from the request's stored dates — elapsed/total are never stored,
/// so there's nothing to keep in sync as days pass.
/// </summary>
public record CurrentLeaveInfo
{
    [JsonPropertyName("leave_type")] public string LeaveType { get; init; } = string.Empty;
    [JsonPropertyName("start_date")] public string StartDate { get; init; } = string.Empty;
    [JsonPropertyName("end_date")] public string EndDate { get; init; } = string.Empty;
    [JsonPropertyName("total_days")] public int TotalDays { get; init; }
    [JsonPropertyName("elapsed_days")] public int ElapsedDays { get; init; }
}
