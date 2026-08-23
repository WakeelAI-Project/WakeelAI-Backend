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

    /// <summary>
    /// Optional override for which document type's clauses to generate (e.g. "Contract",
    /// "Warning_Letter", "Termination_Letter"). Defaults to the template's own document type
    /// when omitted. This only steers AI prompt/retrieval; it never changes the template's
    /// persisted DocumentType, which remains immutable after creation.
    /// </summary>
    [JsonPropertyName("clause_type")]
    public string? ClauseType { get; init; }

    [JsonPropertyName("document_type")]
    public string? DocumentType { get; init; }

    [JsonPropertyName("template_name")]
    public string? TemplateName { get; init; }
}
