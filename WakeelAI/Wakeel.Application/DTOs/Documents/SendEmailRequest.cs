using System.Text.Json.Serialization;

namespace Wakeel.Application.DTOs.Documents;

public class SendEmailRequest
{
    [JsonPropertyName("email_to")]
    public string? EmailTo { get; set; }
}
