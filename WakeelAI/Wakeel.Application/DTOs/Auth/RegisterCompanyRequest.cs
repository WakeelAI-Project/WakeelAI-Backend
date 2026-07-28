using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Wakeel.Application.DTOs.Auth;

/// <summary>
/// Represents the request payload for company and owner registration.
/// Includes company information and admin credentials.
/// Properties use snake_case in JSON via JsonPropertyName attributes.
/// </summary>
public record RegisterCompanyRequest
{
    /// <summary>
    /// The official name of the company to register.
    /// </summary>
    [JsonPropertyName("company_name")]
    [Required(ErrorMessage = "Company name is required.")]
    public string CompanyName { get; init; } = string.Empty;

    /// <summary>
    /// The tax identification number of the company (VAT, Business License ID, etc.).
    /// </summary>
    [JsonPropertyName("tax_id")]
    [Required(ErrorMessage = "Tax ID is required.")]
    public string TaxId { get; init; } = string.Empty;

    /// <summary>
    /// The full name of the company owner/admin.
    /// </summary>
    [JsonPropertyName("owner_full_name")]
    [Required(ErrorMessage = "Owner full name is required.")]
    public string OwnerFullName { get; init; } = string.Empty;

    /// <summary>
    /// The email address of the company owner (must be unique in the system).
    /// </summary>
    [JsonPropertyName("owner_email")]
    [Required(ErrorMessage = "Owner email is required.")]
    [EmailAddress(ErrorMessage = "Owner email must be a valid email address.")]
    public string OwnerEmail { get; init; } = string.Empty;

    /// <summary>
    /// The password for the owner's account.
    /// </summary>
    [JsonPropertyName("password")]
    [Required(ErrorMessage = "Password is required.")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters long.")]
    public string Password { get; init; } = string.Empty;
}
