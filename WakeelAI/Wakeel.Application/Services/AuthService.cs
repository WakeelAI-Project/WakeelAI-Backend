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
    private readonly IEmailSender _emailSender;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator tokenGenerator,
        IRefreshTokenHasher refreshTokenHasher,
        IEmailSender emailSender,
        ILogger<AuthService> logger
    )
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        _tokenGenerator = tokenGenerator ?? throw new ArgumentNullException(nameof(tokenGenerator));
        _refreshTokenHasher = refreshTokenHasher ?? throw new ArgumentNullException(nameof(refreshTokenHasher));
        _emailSender = emailSender ?? throw new ArgumentNullException(nameof(emailSender));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    public async Task<(bool IsSuccess, string? ErrorMessage)> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken);
        if (user is null)
            return (false, "user_not_found");

        if (!_passwordHasher.VerifyPassword(request.CurrentPassword, user.PasswordHash))
            return (false, "invalid_current_password");

        user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
        user.MustChangePassword = false;
        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (true, null);
    }

    // ChangePasswordAsync removed

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
                MustChangePassword = false,
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
                ExpiresIn = _tokenGenerator.AccessTokenExpirationSeconds,
                MustChangePassword = user.MustChangePassword
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

            // Rotation: revoke the presented refresh token — it can never be used again,
            // even if it hasn't expired yet.
            storedToken.IsRevoked = true;
            _unitOfWork.RefreshTokens.Update(storedToken);

            var accessToken = _tokenGenerator.GenerateAccessToken(user.Id, user.Email, user.Role, user.CompanyId);
            var newRefreshToken = _tokenGenerator.GenerateRefreshToken(user.Id);
            await StoreRefreshTokenAsync(user.Id, newRefreshToken, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var response = new RefreshTokenResponse
            {
                AccessToken = accessToken,
                RefreshToken = newRefreshToken,
                ExpiresIn = _tokenGenerator.AccessTokenExpirationSeconds
            };

            _logger.LogInformation("Access and refresh tokens rotated for UserId: {UserId}", user.Id);
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
    /// How long an issued OTP remains valid.
    /// </summary>
    private static readonly TimeSpan OtpValidity = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Number of wrong-code attempts allowed against a single OTP before it is invalidated.
    /// </summary>
    private const int MaxOtpFailedAttempts = 5;

    /// <inheritdoc />
    public async Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        try
        {
            var user = await _unitOfWork.Users.GetByEmailAsync(request.Email, cancellationToken);
            if (user is null || !user.IsActive)
            {
                _logger.LogInformation("Forgot-password OTP requested for unknown or inactive email: {Email}", request.Email);
                return;
            }

            var otp = GenerateOtp();

            // Only the latest OTP is ever valid: drop any previous unexpired one for this user.
            var existingOtps = await _unitOfWork.PasswordResetOtps.FindAsync(o => o.UserId == user.Id, cancellationToken);
            foreach (var existing in existingOtps)
                _unitOfWork.PasswordResetOtps.Remove(existing);

            var record = new PasswordResetOtp
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                OtpHash = _passwordHasher.HashPassword(otp),
                ExpiresAt = DateTime.UtcNow.Add(OtpValidity),
                FailedAttempts = 0,
                CreatedAt = DateTime.UtcNow
            };
            await _unitOfWork.PasswordResetOtps.AddAsync(record, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var subject = "Your Wakeel password reset code";
            var body = $"<p>Hello {user.FullName},</p><p>Your verification code is <strong>{otp}</strong>. It expires in 10 minutes.</p><p>If you didn't request this, you can safely ignore this email.</p>";
            try
            {
                await _emailSender.SendEmailAsync(user.Email, subject, body, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send forgot-password OTP email for UserId: {UserId}", user.Id);
            }

            _logger.LogInformation("Forgot-password OTP issued for UserId: {UserId}", user.Id);
        }
        catch (Exception ex)
        {
            // Swallow: the caller always returns an identical response regardless of
            // outcome, so an internal failure here must not surface to the client.
            _logger.LogError(ex, "Unexpected error during forgot-password flow for email: {Email}", request.Email);
        }
    }

    /// <inheritdoc />
    public async Task<(bool IsSuccess, string? ErrorMessage, AuthResultStatus Status)> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default
    )
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        try
        {
            var user = await _unitOfWork.Users.GetByEmailAsync(request.Email, cancellationToken);
            if (user is null)
                return (false, "invalid_otp", AuthResultStatus.InvalidOtp);

            var record = await _unitOfWork.PasswordResetOtps.FirstOrDefaultAsync(
                o => o.UserId == user.Id,
                cancellationToken);
            if (record is null)
                return (false, "invalid_otp", AuthResultStatus.InvalidOtp);

            if (record.ExpiresAt < DateTime.UtcNow)
            {
                _unitOfWork.PasswordResetOtps.Remove(record);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return (false, "otp_expired", AuthResultStatus.OtpExpired);
            }

            if (!_passwordHasher.VerifyPassword(request.Otp, record.OtpHash))
            {
                record.FailedAttempts++;

                if (record.FailedAttempts >= MaxOtpFailedAttempts)
                {
                    _unitOfWork.PasswordResetOtps.Remove(record);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                    _logger.LogWarning("Forgot-password OTP locked after too many failed attempts. UserId: {UserId}", user.Id);
                    return (false, "too_many_attempts", AuthResultStatus.TooManyOtpAttempts);
                }

                _unitOfWork.PasswordResetOtps.Update(record);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return (false, "invalid_otp", AuthResultStatus.InvalidOtp);
            }

            user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
            _unitOfWork.Users.Update(user);
            _unitOfWork.PasswordResetOtps.Remove(record);

            var activeTokens = await _unitOfWork.RefreshTokens.FindAsync(
                rt => rt.UserId == user.Id && !rt.IsRevoked,
                cancellationToken);
            foreach (var token in activeTokens)
            {
                token.IsRevoked = true;
                _unitOfWork.RefreshTokens.Update(token);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Password reset via OTP completed for UserId: {UserId}", user.Id);
            return (true, null, AuthResultStatus.Success);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during OTP password reset for email: {Email}", request.Email);
            return (false, "An unexpected error occurred while resetting the password.", AuthResultStatus.Failure);
        }
    }

    /// <summary>
    /// Generates a cryptographically random 6-digit numeric OTP, zero-padded (e.g. "042817").
    /// </summary>
    private static string GenerateOtp()
    {
        return System.Security.Cryptography.RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
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