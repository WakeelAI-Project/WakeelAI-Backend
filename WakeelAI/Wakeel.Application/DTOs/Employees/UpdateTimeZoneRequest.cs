using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Wakeel.Application.DTOs.Employees;

public record UpdateTimeZoneRequest
{
    [JsonPropertyName("timezone_id")]
    [Required(ErrorMessage = "timezone_id is required.")]
    [StringLength(100, ErrorMessage = "timezone_id must be at most 100 characters.")]
    public string TimeZoneId { get; init; } = string.Empty;
}
