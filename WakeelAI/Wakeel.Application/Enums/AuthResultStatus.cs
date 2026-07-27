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
    Failure = 3
}