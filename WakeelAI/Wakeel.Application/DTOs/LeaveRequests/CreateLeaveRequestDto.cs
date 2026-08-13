using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Wakeel.Application.DTOs.LeaveRequests;

public class CreateLeaveRequestDto
{
    [JsonPropertyName("leave_type")]
    [Required(ErrorMessage = "leave_type is required.")]
    [RegularExpression("^(Annual|Sick|Unpaid)$", ErrorMessage = "leave_type must be Annual, Sick, or Unpaid.")]
    public string LeaveType { get; set; } = string.Empty;

    [JsonPropertyName("start_date")]
    [Required(ErrorMessage = "start_date is required.")]
    public string StartDate { get; set; } = string.Empty;

    [JsonPropertyName("end_date")]
    [Required(ErrorMessage = "end_date is required.")]
    public string EndDate { get; set; } = string.Empty;

    [JsonPropertyName("reason")]
    [MaxLength(500, ErrorMessage = "reason cannot exceed 500 characters.")]
    public string? Reason { get; set; }

    [JsonPropertyName("attachment_url")]
    public string? AttachmentUrl { get; set; }
}
