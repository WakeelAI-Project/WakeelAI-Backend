using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wakeel.Application.DTOs.Auth;
using Wakeel.Application.Enums;
using Wakeel.Application.Interfaces;
using Wakeel.Application.Interfaces.Repositories;
using Wakeel.Domain.Entities;
using Wakeel.Domain.Enums;

namespace Wakeel.Application.Services;

/// <summary>
/// Handles authentication and company/user registration workflows.
/// </summary>
public class AuthService : IAuthService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _tokenGenerator;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator tokenGenerator,
        ILogger<AuthService> logger
    )
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        _tokenGenerator = tokenGenerator ?? throw new ArgumentNullException(nameof(tokenGenerator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Registers a new company and its associated Owner-role user asynchronously.
    /// Creates a Company entity with an associated Owner-role User and stores hashed credentials.
    /// </summary>
    /// <param name="request">The registration request containing company name and admin credentials.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A tuple containing:
    /// - IsSuccess: Whether the registration was successful.
    /// - Data: The response containing CompanyId, UserId, and tokens (null if failed).
    /// - ErrorMessage: A descriptive error message if registration failed (null if successful).
    /// - Status: An AuthResultStatus enum indicating the result of the operation.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown if request is null.</exception>
    public async Task<(bool IsSuccess, RegisterCompanyResponse? Data, string? ErrorMessage, AuthResultStatus Status)> RegisterCompanyAsync(
        RegisterCompanyRequest request,
        CancellationToken cancellationToken = default
    )
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        try
        {
            _logger.LogInformation("Starting company registration for email: {Email}", request.OwnerEmail);

            // Check email uniqueness before creating company to prevent duplicate key exceptions
            var emailExists = await _unitOfWork.Users.EmailExistsAsync(request.OwnerEmail, cancellationToken);
            if (emailExists)
            {
                _logger.LogWarning("Registration failed: Email already exists - {Email}", request.OwnerEmail);
                return (
                    IsSuccess: false,
                    Data: null,
                    ErrorMessage: "The provided email is already registered. Please use a different email address.",
                    Status: AuthResultStatus.EmailAlreadyExists
                );
            }

            var hashedPassword = _passwordHasher.HashPassword(request.Password);
            _logger.LogDebug("Password hashed successfully for email: {Email}", request.OwnerEmail);

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = request.CompanyName,
                TaxId = request.TaxId,
                Industry = string.Empty,
                Address = string.Empty,
                RegisteredAt = DateTime.UtcNow,
                IsActive = true
            };

            await _unitOfWork.Companies.AddAsync(company, cancellationToken);
            _logger.LogInformation("Company created with ID: {CompanyId}, Name: {CompanyName}", company.Id, company.Name);

            var user = new User
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                Email = request.OwnerEmail,
                PasswordHash = hashedPassword,
                FullName = request.OwnerFullName,
                Phone = string.Empty,
                Role = UserRole.Owner,
                IsActive = true,
                IsEmailConfirmed = false,
                ActivationToken = string.Empty,
                ActivationTokenExpiry = DateTime.UtcNow,
                CreatedByUserId = null,
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.Users.AddAsync(user, cancellationToken);
            _logger.LogInformation("User created with ID: {UserId}, Email: {Email}, Role: Owner", user.Id, user.Email);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Company and User saved to database successfully");

            var accessToken = _tokenGenerator.GenerateAccessToken(user.Id, user.Email, user.Role, company.Id);
            var refreshToken = _tokenGenerator.GenerateRefreshToken(user.Id);
            _logger.LogDebug("Tokens generated for user: {UserId}", user.Id);

            var response = new RegisterCompanyResponse
            {
                CompanyId = company.Id,
                UserId = user.Id,
                Role = user.Role.ToString(),
                AccessToken = accessToken,
                RefreshToken = refreshToken
            };

            _logger.LogInformation("Company registration completed successfully. CompanyId: {CompanyId}, UserId: {UserId}",
                company.Id, user.Id);

            return (
                IsSuccess: true,
                Data: response,
                ErrorMessage: null,
                Status: AuthResultStatus.Success
            );
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error during company registration for email: {Email}", request.OwnerEmail);
            return (
                IsSuccess: false,
                Data: null,
                ErrorMessage: "A database error occurred during registration. Please try again later.",
                Status: AuthResultStatus.Failure
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during company registration for email: {Email}", request.OwnerEmail);
            return (
                IsSuccess: false,
                Data: null,
                ErrorMessage: "An unexpected error occurred during registration. Please try again later.",
                Status: AuthResultStatus.Failure
            );
        }
    }
}