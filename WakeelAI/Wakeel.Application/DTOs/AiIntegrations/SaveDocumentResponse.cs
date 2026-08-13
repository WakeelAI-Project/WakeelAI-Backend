using System.Text.Json.Serialization;

namespace Wakeel.Application.DTOs.AiIntegrations;

public class SaveDocumentResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("documentId")]
    public string DocumentId { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
}
