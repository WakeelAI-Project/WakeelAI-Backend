using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Wakeel.Application.DTOs.Employees;

public record UpdateEmployeeRequest
{
    [JsonPropertyName("full_name")]
    [StringLength(200, ErrorMessage = "Full name must be at most 200 characters.")]
    public string? FullName { get; init; }

    [JsonPropertyName("job_title")]
    [StringLength(200, ErrorMessage = "Job title must be at most 200 characters.")]
    public string? JobTitle { get; init; }

    [JsonPropertyName("hire_date")]
    public DateTime? HireDate { get; init; }

    [JsonPropertyName("salary")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Salary must be greater than zero.")]
    public decimal? Salary { get; init; }

    [JsonPropertyName("contract_type")]
    [StringLength(100, ErrorMessage = "Contract type must be at most 100 characters.")]
    public string? ContractType { get; init; }

    [JsonPropertyName("national_id")]
    [RegularExpression(@"^\d{14}$", ErrorMessage = "National ID must be exactly 14 digits.")]
    public string? NationalId { get; init; }
}
