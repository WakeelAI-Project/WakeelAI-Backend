using System;
using System.Text.Json.Serialization;

namespace Wakeel.Application.DTOs.Company;

public record CompanyProfileDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("tax_id")]
    public string TaxId { get; init; } = string.Empty;

    [JsonPropertyName("industry")]
    public string Industry { get; init; } = string.Empty;

    [JsonPropertyName("address")]
    public string Address { get; init; } = string.Empty;

    [JsonPropertyName("phone_number")]
    public string PhoneNumber { get; init; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; init; } = string.Empty;

    [JsonPropertyName("logo_url")]
    public string LogoUrl { get; init; } = string.Empty;

    [JsonPropertyName("working_hours")]
    public string WorkingHours { get; init; } = string.Empty;

    [JsonPropertyName("registered_at")]
    public DateTime RegisteredAt { get; init; }
}
