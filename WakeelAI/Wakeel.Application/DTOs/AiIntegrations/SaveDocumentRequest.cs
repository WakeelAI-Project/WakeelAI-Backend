using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Wakeel.Application.DTOs.AiIntegrations;

public class SaveDocumentRequest
{
    [Required]
    [JsonPropertyName("document_type")]
    public string DocumentType { get; set; } = string.Empty;

    [Required]
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [Required]
    [JsonPropertyName("content_html")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("employee_id")]
    public string? EmployeeId { get; set; }

    [JsonPropertyName("template_id")]
    public string? TemplateId { get; set; }

    [JsonPropertyName("metadata")]
    public object? Metadata { get; set; }
}
