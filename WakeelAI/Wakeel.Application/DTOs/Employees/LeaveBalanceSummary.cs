using System.Text.Json.Serialization;

namespace Wakeel.Application.DTOs.Employees;

public record LeaveTypeBalance
{
    [JsonPropertyName("total_days")] public int? TotalDays { get; init; }
    [JsonPropertyName("used_days")] public int UsedDays { get; init; }
    [JsonPropertyName("remaining_days")] public int? RemainingDays { get; init; }
}

public record LeaveBalanceSummary
{
    [JsonPropertyName("annual")] public LeaveTypeBalance? Annual { get; init; }
    [JsonPropertyName("sick")] public LeaveTypeBalance? Sick { get; init; }
    [JsonPropertyName("unpaid")] public LeaveTypeBalance? Unpaid { get; init; }
}
