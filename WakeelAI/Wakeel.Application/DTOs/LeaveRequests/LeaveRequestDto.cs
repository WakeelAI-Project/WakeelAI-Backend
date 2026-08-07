using System;
using System.Text.Json.Serialization;

namespace Wakeel.Application.DTOs.LeaveRequests;

public class LeaveRequestDto
{
    [JsonPropertyName("request_id")]
    public Guid RequestId { get; set; }

    [JsonPropertyName("employee_id")]
    public Guid EmployeeId { get; set; }

    [JsonPropertyName("employee_name")]
    public string? EmployeeName { get; set; }

    [JsonPropertyName("leave_type")]
    public string LeaveType { get; set; } = string.Empty;

    [JsonPropertyName("start_date")]
    public string StartDate { get; set; } = string.Empty;

    [JsonPropertyName("end_date")]
    public string EndDate { get; set; } = string.Empty;

    [JsonPropertyName("days_requested")]
    public int DaysRequested { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    [JsonPropertyName("hr_note")]
    public string? HrNote { get; set; }

    [JsonPropertyName("attachment_url")]
    public string? AttachmentUrl { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("submitted_at")]
    public DateTime? SubmittedAt { get; set; }

    [JsonPropertyName("reviewed_at")]
    public DateTime? ReviewedAt { get; set; }
}
