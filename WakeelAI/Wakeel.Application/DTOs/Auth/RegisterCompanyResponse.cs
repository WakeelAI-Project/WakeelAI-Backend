namespace Wakeel.Application.DTOs.Auth;

/// <summary>
/// Represents the successful response from a company registration request.
/// </summary>
/// <param name="CompanyId">The unique identifier of the newly created company.</param>
/// <param name="UserId">The unique identifier of the admin user created for this company.</param>
/// <param name="Message">A success message describing the registration result.</param>
public record RegisterCompanyResponse(
    Guid CompanyId,
    Guid UserId,
    string Message
);
