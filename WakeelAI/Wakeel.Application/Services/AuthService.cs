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
/// Handles authentication workflows: company/owner registration, login,
/// access-token refresh, and logout (refresh-token revocation).
/// </summary>
public class AuthService : IAuthService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _tokenGenerator;
    private readonly IRefreshTokenHasher _refreshTokenHasher;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator tokenGenerator,
        IRefreshTokenHasher refreshTokenHasher,
        ILogger<AuthService> logger
    )
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        _tokenGenerator = tokenGenerator ?? throw new ArgumentNullException(nameof(tokenGenerator));
        _refreshTokenHasher = refreshTokenHasher ?? throw new ArgumentNullException(nameof(refreshTokenHasher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
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

            var user = new User
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                Email = request.OwnerEmail,
                PasswordHash = hashedPassword,
                FullName = request.OwnerFullName,
                Phone = string.Empty,
                Role = UserRole.Company_Owner,
                IsActive = true,
                IsEmailConfirmed = false,
                ActivationToken = string.Empty,
                ActivationTokenExpiry = DateTime.UtcNow,
                CreatedByUserId = null,
                CreatedAt = DateTime.UtcNow
            };
            await _unitOfWork.Users.AddAsync(user, cancellationToken);

            var accessToken = _tokenGenerator.GenerateAccessToken(user.Id, user.Email, user.Role, company.Id);
            var refreshToken = _tokenGenerator.GenerateRefreshToken(user.Id);
            await StoreRefreshTokenAsync(user.Id, refreshToken, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Company registration completed. CompanyId: {CompanyId}, UserId: {UserId}", company.Id, user.Id);

            var response = new RegisterCompanyResponse
            {
                CompanyId = company.Id,
                UserId = user.Id,
                Role = user.Role.ToString(),
                AccessToken = accessToken,
                RefreshToken = refreshToken
            };

            return (true, response, null, AuthResultStatus.Success);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error during company registration for email: {Email}", request.OwnerEmail);
            return (false, null, "A database error occurred during registration. Please try again later.", AuthResultStatus.Failure);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during company registration for email: {Email}", request.OwnerEmail);
            return (false, null, "An unexpected error occurred during registration. Please try again later.", AuthResultStatus.Failure);
        }
    }

    /// <inheritdoc />
    public async Task<(bool IsSuccess, LoginResponse? Data, string? ErrorMessage, AuthResultStatus Status)> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default
    )
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        try
        {
            _logger.LogInformation("Login attempt for email: {Email}", request.Email);

            var user = await _unitOfWork.Users.GetByEmailAsync(request.Email, cancellationToken);
            if (user is null || !_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
            {
                _logger.LogWarning("Login failed: invalid credentials for email: {Email}", request.Email);
                return (false, null, "invalid_credentials", AuthResultStatus.InvalidCredentials);
            }

            if (!user.IsActive)
            {
                _logger.LogWarning("Login failed: account inactive for email: {Email}", request.Email);
                return (false, null, "account_inactive", AuthResultStatus.AccountInactive);
            }

            var accessToken = _tokenGenerator.GenerateAccessToken(user.Id, user.Email, user.Role, user.CompanyId);
            var refreshToken = _tokenGenerator.GenerateRefreshToken(user.Id);
            await StoreRefreshTokenAsync(user.Id, refreshToken, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var response = new LoginResponse
            {
                UserId = user.Id,
                CompanyId = user.CompanyId,
                Role = user.Role.ToString(),
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresIn = _tokenGenerator.AccessTokenExpirationSeconds
            };

            _logger.LogInformation("Login succeeded for UserId: {UserId}", user.Id);
            return (true, response, null, AuthResultStatus.Success);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during login for email: {Email}", request.Email);
            return (false, null, "An unexpected error occurred during login. Please try again later.", AuthResultStatus.Failure);
        }
    }

    /// <inheritdoc />
    public async Task<(bool IsSuccess, RefreshTokenResponse? Data, string? ErrorMessage, AuthResultStatus Status)> RefreshTokenAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken = default
    )
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        try
        {
            var tokenHash = _refreshTokenHasher.Hash(request.RefreshToken);
            var storedToken = await _unitOfWork.RefreshTokens.GetByTokenHashAsync(tokenHash, cancellationToken);

            if (storedToken is null || storedToken.IsRevoked)
            {
                _logger.LogWarning("Refresh failed: token not found or revoked.");
                return (false, null, "Invalid refresh token.", AuthResultStatus.InvalidRefreshToken);
            }

            if (storedToken.ExpiresAt < DateTime.UtcNow)
            {
                _logger.LogWarning("Refresh failed: token expired for UserId: {UserId}", storedToken.UserId);
                return (false, null, "Refresh token has expired. Please log in again.", AuthResultStatus.RefreshTokenExpired);
            }

            var user = await _unitOfWork.Users.GetByIdAsync(storedToken.UserId, cancellationToken);
            if (user is null || !user.IsActive)
            {
                _logger.LogWarning("Refresh failed: user not found or inactive. UserId: {UserId}", storedToken.UserId);
                return (false, null, "Account not found or inactive.", AuthResultStatus.AccountInactive);
            }

            var accessToken = _tokenGenerator.GenerateAccessToken(user.Id, user.Email, user.Role, user.CompanyId);

            var response = new RefreshTokenResponse
            {
                AccessToken = accessToken,
                ExpiresIn = _tokenGenerator.AccessTokenExpirationSeconds
            };

            _logger.LogInformation("Access token refreshed for UserId: {UserId}", user.Id);
            return (true, response, null, AuthResultStatus.Success);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during token refresh.");
            return (false, null, "An unexpected error occurred while refreshing the token.", AuthResultStatus.Failure);
        }
    }

    /// <inheritdoc />
    public async Task<(bool IsSuccess, string? ErrorMessage, AuthResultStatus Status)> LogoutAsync(
        LogoutRequest request,
        CancellationToken cancellationToken = default
    )
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        try
        {
            var tokenHash = _refreshTokenHasher.Hash(request.RefreshToken);
            var storedToken = await _unitOfWork.RefreshTokens.GetByTokenHashAsync(tokenHash, cancellationToken);

            if (storedToken is null)
            {
                // Don't reveal whether the token existed — logout is idempotent either way.
                return (true, null, AuthResultStatus.Success);
            }

            storedToken.IsRevoked = true;
            _unitOfWork.RefreshTokens.Update(storedToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Refresh token revoked for UserId: {UserId}", storedToken.UserId);
            return (true, null, AuthResultStatus.Success);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during logout.");
            return (false, "An unexpected error occurred during logout.", AuthResultStatus.Failure);
        }
    }

    /// <summary>
    /// Hashes and persists a newly generated refresh token for the given user.
    /// Does not call SaveChangesAsync — the caller commits as part of its own unit of work.
    /// </summary>
    private async Task StoreRefreshTokenAsync(Guid userId, string rawRefreshToken, CancellationToken cancellationToken)
    {
        var refreshTokenEntity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = _refreshTokenHasher.Hash(rawRefreshToken),
            ExpiresAt = DateTime.UtcNow.AddDays(_tokenGenerator.RefreshTokenExpirationDays),
            IsRevoked = false,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.RefreshTokens.AddAsync(refreshTokenEntity, cancellationToken);
    }
}