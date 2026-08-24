using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Wakeel.Application.DTOs.Employees;

/// <summary>
/// A data subject's own personal-data bundle (FIX-25). Intentionally omits the password
/// hash, activation token, and generated-document body content - the export covers the
/// employee's own records, not internal security material or full document text.
/// </summary>
public record PersonalDataExportResponse
{
    [JsonPropertyName("exported_at")] public DateTime ExportedAt { get; init; }
    [JsonPropertyName("user")] public PersonalDataExportUser User { get; init; } = null!;
    [JsonPropertyName("profile")] public PersonalDataExportProfile Profile { get; init; } = null!;
    [JsonPropertyName("leave_balances")] public IReadOnlyList<PersonalDataExportLeaveBalance> LeaveBalances { get; init; } = [];
    [JsonPropertyName("leave_requests")] public IReadOnlyList<PersonalDataExportLeaveRequest> LeaveRequests { get; init; } = [];
    [JsonPropertyName("generated_documents")] public IReadOnlyList<PersonalDataExportDocument> GeneratedDocuments { get; init; } = [];
}

public record PersonalDataExportUser
{
    [JsonPropertyName("user_id")] public Guid UserId { get; init; }
    [JsonPropertyName("full_name")] public string FullName { get; init; } = string.Empty;
    [JsonPropertyName("email")] public string Email { get; init; } = string.Empty;
    [JsonPropertyName("phone")] public string Phone { get; init; } = string.Empty;
    [JsonPropertyName("role")] public string Role { get; init; } = string.Empty;
    [JsonPropertyName("employment_status")] public string EmploymentStatus { get; init; } = string.Empty;
    [JsonPropertyName("created_at")] public DateTime CreatedAt { get; init; }
}

public record PersonalDataExportProfile
{
    [JsonPropertyName("job_title")] public string JobTitle { get; init; } = string.Empty;
    [JsonPropertyName("department")] public string? Department { get; init; }
    [JsonPropertyName("national_id")] public string? NationalId { get; init; }
    [JsonPropertyName("hire_date")] public DateOnly HireDate { get; init; }
    [JsonPropertyName("salary")] public decimal Salary { get; init; }
    [JsonPropertyName("contract_type")] public string ContractType { get; init; } = string.Empty;
    [JsonPropertyName("timezone_id")] public string? TimeZoneId { get; init; }
}

public record PersonalDataExportLeaveBalance
{
    [JsonPropertyName("leave_type")] public string LeaveType { get; init; } = string.Empty;
    [JsonPropertyName("year")] public int Year { get; init; }
    [JsonPropertyName("total_days")] public int? TotalDays { get; init; }
    [JsonPropertyName("used_days")] public int UsedDays { get; init; }
}

public record PersonalDataExportLeaveRequest
{
    [JsonPropertyName("request_id")] public Guid RequestId { get; init; }
    [JsonPropertyName("leave_type")] public string LeaveType { get; init; } = string.Empty;
    [JsonPropertyName("start_date")] public DateOnly StartDate { get; init; }
    [JsonPropertyName("end_date")] public DateOnly EndDate { get; init; }
    [JsonPropertyName("days_requested")] public int DaysRequested { get; init; }
    [JsonPropertyName("status")] public string Status { get; init; } = string.Empty;
    [JsonPropertyName("reason")] public string? Reason { get; init; }
    [JsonPropertyName("created_at")] public DateTime CreatedAt { get; init; }
}

public record PersonalDataExportDocument
{
    [JsonPropertyName("document_id")] public Guid DocumentId { get; init; }
    [JsonPropertyName("document_type")] public string DocumentType { get; init; } = string.Empty;
    [JsonPropertyName("title")] public string Title { get; init; } = string.Empty;
    [JsonPropertyName("status")] public string Status { get; init; } = string.Empty;
    [JsonPropertyName("created_at")] public DateTime CreatedAt { get; init; }
}
