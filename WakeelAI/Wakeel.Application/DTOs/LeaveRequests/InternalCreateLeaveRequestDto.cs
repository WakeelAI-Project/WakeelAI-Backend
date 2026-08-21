using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Wakeel.Application.DTOs.LeaveRequests;

/// <summary>
/// Request DTO used exclusively by the internal M2M AI endpoint (POST /api/ai/leave-requests).
/// Identity (employee_id, company_id) is NEVER accepted from this body;
/// it is extracted strictly from the trusted X-User-Id / X-Company-Id headers
/// by the InternalApiKeyMiddleware and InternalAiLeaveController.
///
/// For Sick leave, the attachment must be pre-uploaded via POST /api/leave-requests/attachments
/// and the returned URL passed in the attachment_url field here.
/// </summary>
public record InternalCreateLeaveRequestDto
{
    /// <summary>The type of leave being requested. Must be Annual, Sick, or Unpaid.</summary>
    [JsonPropertyName("leave_type")]
    [Required(ErrorMessage = "leave_type is required.")]
    [RegularExpression("(?i)^(Annual|Sick|Unpaid)(_leave|\\sleave|leave)?$", ErrorMessage = "leave_type must be Annual, Sick, or Unpaid.")]
    public string LeaveType { get; init; } = string.Empty;

    /// <summary>The start date of the leave period in yyyy-MM-dd format.</summary>
    [JsonPropertyName("start_date")]
    [Required(ErrorMessage = "start_date is required.")]
    public string StartDate { get; init; } = string.Empty;

    /// <summary>The end date of the leave period in yyyy-MM-dd format.</summary>
    [JsonPropertyName("end_date")]
    [Required(ErrorMessage = "end_date is required.")]
    public string EndDate { get; init; } = string.Empty;

    /// <summary>Optional reason for the leave request.</summary>
    [JsonPropertyName("reason")]
    [MaxLength(500, ErrorMessage = "reason cannot exceed 500 characters.")]
    public string? Reason { get; init; }

    /// <summary>
    /// Required when leave_type is Sick. Must be the URL returned by
    /// POST /api/leave-requests/attachments (the pre-upload step).
    /// This field must NOT contain an employee_id; identity comes from headers only.
    /// </summary>
    [JsonPropertyName("attachment_url")]
    public string? AttachmentUrl { get; init; }
}
