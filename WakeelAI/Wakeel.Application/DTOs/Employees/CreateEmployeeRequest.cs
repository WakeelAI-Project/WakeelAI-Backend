using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Wakeel.Application.DTOs.Employees;

public record CreateEmployeeRequest
{
    [JsonPropertyName("full_name")]
    [Required(ErrorMessage = "Full name is required.")]
    [StringLength(200, ErrorMessage = "Full name must be at most 200 characters.")]
    public string FullName { get; init; } = string.Empty;

    [JsonPropertyName("email")]
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Email must be a valid email address.")]
    [StringLength(256, ErrorMessage = "Email must be at most 256 characters.")]
    public string Email { get; init; } = string.Empty;

    [JsonPropertyName("job_title")]
    [Required(ErrorMessage = "Job title is required.")]
    [StringLength(200, ErrorMessage = "Job title must be at most 200 characters.")]
    public string JobTitle { get; init; } = string.Empty;

    [JsonPropertyName("hire_date")]
    [Required(ErrorMessage = "Hire date is required.")]
    public DateTime HireDate { get; init; }

    [JsonPropertyName("salary")]
    [Required(ErrorMessage = "Salary is required.")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Salary must be greater than zero.")]
    public decimal Salary { get; init; }

    [JsonPropertyName("contract_type")]
    [Required(ErrorMessage = "Contract type is required.")]
    [StringLength(100, ErrorMessage = "Contract type must be at most 100 characters.")]
    public string ContractType { get; init; } = string.Empty;
}
