using System.Text.Json.Serialization;

namespace Wakeel.Application.DTOs.Templates;

public class UpdateTemplateRequest
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("content_template")]
    public string? ContentTemplate { get; set; }

    [JsonPropertyName("is_active")]
    public bool? IsActive { get; set; }
}
