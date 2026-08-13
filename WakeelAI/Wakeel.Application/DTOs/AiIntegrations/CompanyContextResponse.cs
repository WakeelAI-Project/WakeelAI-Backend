using System.Text.Json.Serialization;

namespace Wakeel.Application.DTOs.AiIntegrations;

public class CompanyContextResponse
{
    [JsonPropertyName("companyId")]
    public string CompanyId { get; set; } = string.Empty;

    [JsonPropertyName("companyName")]
    public string CompanyName { get; set; } = string.Empty;

    [JsonPropertyName("industry")]
    public string? Industry { get; set; }

    [JsonPropertyName("workingHours")]
    public string? WorkingHours { get; set; }

    [JsonPropertyName("policyAvailable")]
    public bool PolicyAvailable { get; set; }
}
