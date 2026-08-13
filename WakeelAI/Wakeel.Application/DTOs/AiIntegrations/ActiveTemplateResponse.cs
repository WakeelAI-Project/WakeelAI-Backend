using System.Text.Json.Serialization;

namespace Wakeel.Application.DTOs.AiIntegrations;

public class ActiveTemplateResponse
{
    [JsonPropertyName("template_id")]
    public string TemplateId { get; set; } = string.Empty;

    [JsonPropertyName("document_type")]
    public string DocumentType { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("content_template")]
    public string ContentTemplate { get; set; } = string.Empty;
}
