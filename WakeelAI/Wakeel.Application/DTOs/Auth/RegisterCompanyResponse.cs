using System.Text.Json.Serialization;

namespace Wakeel.Application.DTOs.Auth;

/// <summary>
/// Represents the successful response from a company registration request.
/// Includes company and user identifiers along with authentication tokens.
/// Properties use snake_case in JSON via JsonPropertyName attributes.
/// </summary>
public record RegisterCompanyResponse
{
    /// <summary>
    /// The unique identifier of the newly created company.
    /// </summary>
    [JsonPropertyName("company_id")]
    public Guid CompanyId { get; init; }

    /// <summary>
    /// The unique identifier of the newly created owner/admin user.
    /// </summary>
    [JsonPropertyName("user_id")]
    public Guid UserId { get; init; }

    /// <summary>
    /// The role assigned to the user (e.g., "Owner").
    /// </summary>
    [JsonPropertyName("role")]
    public string Role { get; init; } = string.Empty;

    /// <summary>
    /// The JWT access token for API authentication.
    /// Used in the Authorization header: "Authorization: Bearer {access_token}".
    /// </summary>
    [JsonPropertyName("access_token")]
    public string AccessToken { get; init; } = string.Empty;

    /// <summary>
    /// The JWT refresh token for obtaining a new access token when it expires.
    /// </summary>
    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; init; } = string.Empty;
}
