using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Wakeel.Application.DTOs.Employees;

/// <summary>
/// HR manual override of a leave balance's cap for one year - the mechanism for the two
/// statutory Annual tiers this system does not compute automatically (age 50+, disability),
/// and for any other one-off adjustment HR needs to make.
/// </summary>
public record AdjustLeaveBalanceRequest
{
    [JsonPropertyName("year")]
    [Required(ErrorMessage = "year is required.")]
    [Range(2000, 2200, ErrorMessage = "year must be a valid calendar year.")]
    public int Year { get; init; }

    /// <summary>Null means uncapped. Otherwise must be at least the balance's already-used days.</summary>
    [JsonPropertyName("total_days")]
    public int? TotalDays { get; init; }
}
