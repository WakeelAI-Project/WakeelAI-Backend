using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Wakeel.Application.DTOs.Employees;

public record CreateEmployeeRequest
{
    [JsonPropertyName("full_name")] [Required] public string FullName { get; init; } = string.Empty;
    [JsonPropertyName("email")] [Required] [EmailAddress] public string Email { get; init; } = string.Empty;
    [JsonPropertyName("job_title")] [Required] public string JobTitle { get; init; } = string.Empty;
    [JsonPropertyName("hire_date")] [Required] public DateTime HireDate { get; init; }
    [JsonPropertyName("salary")] [Required] public decimal Salary { get; init; }
    [JsonPropertyName("contract_type")] [Required] public string ContractType { get; init; } = string.Empty;
}
