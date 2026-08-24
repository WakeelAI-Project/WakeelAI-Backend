using System.Text.Json.Serialization;

namespace Wakeel.Application.DTOs.Employees;

public record LeaveTypeBalance
{
    [JsonPropertyName("total_days")] public int? TotalDays { get; init; }
    [JsonPropertyName("used_days")] public int UsedDays { get; init; }
    [JsonPropertyName("remaining_days")] public int? RemainingDays { get; init; }

    /// <summary>Days held by the employee's own Pending requests of this type this year -
    /// already netted out of <see cref="RemainingDays"/>, surfaced separately so a client
    /// can explain why remaining is lower than total minus used.</summary>
    [JsonPropertyName("reserved_days")] public int ReservedDays { get; init; }
}

public record LeaveBalanceSummary
{
    [JsonPropertyName("annual")] public LeaveTypeBalance? Annual { get; init; }
    [JsonPropertyName("sick")] public LeaveTypeBalance? Sick { get; init; }
    [JsonPropertyName("unpaid")] public LeaveTypeBalance? Unpaid { get; init; }
}
