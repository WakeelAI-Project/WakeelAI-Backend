using System.Text.Json.Serialization;

namespace Wakeel.Application.DTOs.Documents;

public class UpdateDocumentRequest
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("content_html")]
    public string? ContentHtml { get; set; }
}
