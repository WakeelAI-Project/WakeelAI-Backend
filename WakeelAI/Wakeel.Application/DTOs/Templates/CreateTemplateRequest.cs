using System.Text.Json.Serialization;

namespace Wakeel.Application.DTOs.Templates;

public class CreateTemplateRequest
{
    [JsonPropertyName("document_type")]
    public string DocumentType { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("content_template")]
    public string ContentTemplate { get; set; } = string.Empty;

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; }
}
