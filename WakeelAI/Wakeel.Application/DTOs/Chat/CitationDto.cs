using System.Text.Json.Serialization;

namespace Wakeel.Application.DTOs.Chat;

/// <summary>
/// Structured source citation returned by the AI service.
/// </summary>
public class CitationDto
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("section")]
    public string? Section { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }
}
