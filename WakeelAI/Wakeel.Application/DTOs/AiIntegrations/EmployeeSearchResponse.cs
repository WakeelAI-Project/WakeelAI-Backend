using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Wakeel.Application.DTOs.AiIntegrations;

public class EmployeeSearchResponse
{
    [JsonPropertyName("employees")]
    public List<EmployeeSearchResultDto> Employees { get; set; } = new();
}

public class EmployeeSearchResultDto
{
    [JsonPropertyName("employee_id")]
    public string EmployeeId { get; set; } = string.Empty;

    [JsonPropertyName("full_name")]
    public string FullName { get; set; } = string.Empty;
}
