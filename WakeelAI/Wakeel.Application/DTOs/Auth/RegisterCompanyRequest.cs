using System.ComponentModel.DataAnnotations;

namespace Wakeel.Application.DTOs.Auth;

/// <summary>
/// Represents the request payload for company registration with admin credentials.
/// Validation is enforced via Data Annotations attributes.
/// </summary>
/// <param name="CompanyName">The name of the company to register (required, non-empty).</param>
/// <param name="AdminEmail">The email address of the company's admin user (required, valid email format, must be unique).</param>
/// <param name="AdminPassword">The password for the admin user (required, minimum 8 characters).</param>
public record RegisterCompanyRequest(
    [Required(ErrorMessage = "Company name is required.")]
    string CompanyName,

    [Required(ErrorMessage = "Admin email is required.")]
    [EmailAddress(ErrorMessage = "Admin email must be a valid email address.")]
    string AdminEmail,

    [Required(ErrorMessage = "Admin password is required.")]
    [MinLength(8, ErrorMessage = "Admin password must be at least 8 characters long.")]
    string AdminPassword
);
