using System;
using System.Text.Json.Serialization;

namespace Wakeel.Application.DTOs.AiIntegrations;

public class CompanyContextResponse
{
    [JsonPropertyName("id")]
    public string CompanyId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string CompanyName { get; set; } = string.Empty;

    [JsonPropertyName("tax_id")]
    public string? TaxId { get; set; }

    [JsonPropertyName("industry")]
    public string? Industry { get; set; }

    [JsonPropertyName("address")]
    public string? Address { get; set; }

    [JsonPropertyName("phone_number")]
    public string? PhoneNumber { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("logo_url")]
    public string? LogoUrl { get; set; }

    [JsonPropertyName("working_hours")]
    public string? WorkingHours { get; set; }

    [JsonPropertyName("registered_at")]
    public DateTime RegisteredAt { get; set; }

    [JsonPropertyName("policy_available")]
    public bool PolicyAvailable { get; set; }
}
