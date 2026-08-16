using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Wakeel.Application.DTOs.Templates;

public record NodeTemplateClausesResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("clauses")]
    public List<GeneratedClauseDto> Clauses { get; init; } = new();
}

public record GeneratedClauseDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; init; } = string.Empty;

    // "labor_law" | "company_policy" | "mixed"
    [JsonPropertyName("category")]
    public string Category { get; init; } = string.Empty;

    [JsonPropertyName("language")]
    public string Language { get; init; } = "en";

    [JsonPropertyName("sources")]
    public List<GeneratedClauseSourceDto> Sources { get; init; } = new();
}

public record GeneratedClauseSourceDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    // "labor-law" | "company-policy"
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("score")]
    public double? Score { get; init; }

    [JsonPropertyName("metadata")]
    public object? Metadata { get; init; }
}
