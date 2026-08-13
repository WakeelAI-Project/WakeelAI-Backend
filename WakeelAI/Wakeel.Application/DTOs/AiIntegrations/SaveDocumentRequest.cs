using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Wakeel.Application.DTOs.AiIntegrations;

public class SaveDocumentRequest
{
    [Required]
    [JsonPropertyName("documentType")]
    public string DocumentType { get; set; } = string.Empty;

    [Required]
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [Required]
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [Required]
    [JsonPropertyName("metadata")]
    public SaveDocumentMetadata Metadata { get; set; } = null!;
}

public class SaveDocumentMetadata
{
    [Required]
    [JsonPropertyName("employeeId")]
    public string EmployeeId { get; set; } = string.Empty;

    [Required]
    [JsonPropertyName("companyId")]
    public string CompanyId { get; set; } = string.Empty;
}
