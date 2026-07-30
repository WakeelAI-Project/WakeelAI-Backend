namespace Wakeel.Application.Enums;

/// <summary>
/// Represents the result status of authentication operations.
/// Used to communicate operation outcomes without HTTP status codes in the Application layer.
/// </summary>
public enum AuthResultStatus
{
    /// <summary>
    /// Operation completed successfully.
    /// </summary>
    Success = 0,

    /// <summary>
    /// The provided email address is already registered in the system.
    /// </summary>
    EmailAlreadyExists = 1,

    /// <summary>
    /// Validation of the request data failed.
    /// </summary>
    ValidationError = 2,

    /// <summary>
    /// Operation failed due to an unexpected error.
    /// </summary>
    Failure = 3,
    
    /// <summary>
    /// The provided credentials (email/password) are invalid.
    /// </summary>
    InvalidCredentials = 4,

    /// <summary>
    /// The user account is inactive and cannot be used for authentication.
    /// </summary>
    AccountInactive = 5,

    /// <summary>
    /// The provided refresh token is invalid or does not match the stored token.
    /// </summary>
    InvalidRefreshToken = 6,

    /// <summary>
    /// The provided refresh token has expired and cannot be used to obtain new access tokens.
    /// </summary>
    RefreshTokenExpired = 7
}