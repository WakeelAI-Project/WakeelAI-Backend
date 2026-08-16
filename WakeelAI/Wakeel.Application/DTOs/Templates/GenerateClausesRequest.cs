using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Wakeel.Application.DTOs.Templates;

public record GenerateClausesRequest
{
    [JsonPropertyName("language")]
    public string Language { get; init; } = "en";

    [JsonPropertyName("include_labor_law")]
    public bool IncludeLaborLaw { get; init; } = true;

    [JsonPropertyName("include_company_policy")]
    public bool IncludeCompanyPolicy { get; init; } = true;

    [JsonPropertyName("instruction")]
    [MaxLength(1000, ErrorMessage = "instruction cannot exceed 1000 characters.")]
    public string? Instruction { get; init; }
}
