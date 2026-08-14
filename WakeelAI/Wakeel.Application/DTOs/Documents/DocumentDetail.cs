using System;
using System.Text.Json.Serialization;

namespace Wakeel.Application.DTOs.Documents;

public class DocumentDetail
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("document_type")]
    public string DocumentType { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("content_html")]
    public string? ContentHtml { get; set; }

    [JsonPropertyName("pdf_url")]
    public string? PdfUrl { get; set; }

    [JsonPropertyName("employee_id")]
    public Guid? EmployeeId { get; set; }

    [JsonPropertyName("template_id")]
    public Guid? TemplateId { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; }
    
    [JsonPropertyName("finalized_at")]
    public DateTime? FinalizedAt { get; set; }
}
