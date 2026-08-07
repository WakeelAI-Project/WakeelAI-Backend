using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Wakeel.Application.DTOs.LeaveRequests;

public class ReviewLeaveRequestDto
{
    [JsonPropertyName("status")]
    [Required(ErrorMessage = "status is required.")]
    [RegularExpression("^(Approved|Rejected)$", ErrorMessage = "status must be Approved or Rejected.")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("hr_note")]
    public string? HrNote { get; set; }
}
