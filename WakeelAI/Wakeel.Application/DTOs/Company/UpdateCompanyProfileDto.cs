using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Wakeel.Application.DTOs.Company;

public record UpdateCompanyProfileDto
{
    [JsonPropertyName("address")]
    public string Address { get; init; } = string.Empty;

    [JsonPropertyName("phone_number")]
    public string PhoneNumber { get; init; } = string.Empty;

    [JsonPropertyName("email")]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;

    [JsonPropertyName("industry")]
    public string Industry { get; init; } = string.Empty;

    [JsonPropertyName("working_hours")]
    public string WorkingHours { get; init; } = string.Empty;
}
