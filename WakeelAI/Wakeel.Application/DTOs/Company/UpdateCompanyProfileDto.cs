using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Wakeel.Application.DTOs.Company;

public record UpdateCompanyProfileDto
{
    [JsonPropertyName("address")]
    public string? Address { get; init; }
    public bool IsAddressProvided { get; init; }

    [JsonPropertyName("phone_number")]
    public string? PhoneNumber { get; init; }
    public bool IsPhoneNumberProvided { get; init; }

    [JsonPropertyName("email")]
    [EmailAddress]
    public string? Email { get; init; }
    public bool IsEmailProvided { get; init; }

    [JsonPropertyName("industry")]
    public string? Industry { get; init; }
    public bool IsIndustryProvided { get; init; }

    [JsonPropertyName("working_hours")]
    public string? WorkingHours { get; init; }
    public bool IsWorkingHoursProvided { get; init; }
}
