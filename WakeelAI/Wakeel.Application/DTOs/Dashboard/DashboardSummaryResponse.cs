using System.Text.Json.Serialization;

namespace Wakeel.Application.DTOs.Dashboard;

public record DashboardSummaryResponse
{
    [JsonPropertyName("employee_count")] public int EmployeeCount { get; init; }
    [JsonPropertyName("active_employees")] public int ActiveEmployees { get; init; }
    [JsonPropertyName("pending_leave_requests")] public int PendingLeaveRequests { get; init; }
    [JsonPropertyName("employees_on_leave_today")] public int EmployeesOnLeaveToday { get; init; }
    [JsonPropertyName("generated_documents_count")] public int GeneratedDocumentsCount { get; init; }
}
