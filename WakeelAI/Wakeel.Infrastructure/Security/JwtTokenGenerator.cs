using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Wakeel.Application.Interfaces;
using Wakeel.Domain.Enums;

namespace Wakeel.Infrastructure.Security;

/// <summary>
/// Generates JWT access tokens (payload: user_id, company_id, role, exp per API spec)
/// and opaque refresh tokens. Reads signing key, issuer, audience, and expirations from configuration.
/// </summary>
public class JwtTokenGenerator : IJwtTokenGenerator
{
    private const int MinimumSecretKeyLengthBytes = 32; // 256 bits, required for HMAC-SHA256
    private const int DefaultAccessTokenExpirationMinutes = 15;
    private const int DefaultRefreshTokenExpirationDays = 7;


    private readonly string _secretKey;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _accessTokenExpirationMinutes;
    private readonly int _refreshTokenExpirationDays;


    public JwtTokenGenerator(IConfiguration configuration)
    {
        if (configuration is null)
            throw new ArgumentNullException(nameof(configuration));

        _secretKey = configuration["Jwt:SecretKey"]
            ?? throw new InvalidOperationException("Jwt:SecretKey is not configured.");
        _issuer = configuration["Jwt:Issuer"]
            ?? throw new InvalidOperationException("Jwt:Issuer is not configured.");
        _audience = configuration["Jwt:Audience"]
            ?? throw new InvalidOperationException("Jwt:Audience is not configured.");

        if (Encoding.UTF8.GetByteCount(_secretKey) < MinimumSecretKeyLengthBytes)
            throw new InvalidOperationException(
                $"Jwt:SecretKey must be at least {MinimumSecretKeyLengthBytes} characters long for HMAC-SHA256 signing.");

        _accessTokenExpirationMinutes = configuration.GetValue<int?>("Jwt:AccessTokenExpirationMinutes")
            ?? DefaultAccessTokenExpirationMinutes;
        _refreshTokenExpirationDays = configuration.GetValue<int?>("Jwt:RefreshTokenExpirationDays")
            ?? DefaultRefreshTokenExpirationDays;
    }

    public int AccessTokenExpirationSeconds => _accessTokenExpirationMinutes * 60;
    public int RefreshTokenExpirationDays => _refreshTokenExpirationDays;

    /// <inheritdoc />
    public string GenerateAccessToken(Guid userId, string email, UserRole role, Guid companyId)
    {
        var claims = new List<Claim>
        {
            new("user_id", userId.ToString()),
            new("company_id", companyId.ToString()),
            new("role", role.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
        var signingCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_accessTokenExpirationMinutes),
            signingCredentials: signingCredentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <inheritdoc />
    public string GenerateRefreshToken(Guid userId)
    {
        // Opaque, cryptographically random token. Caller (AuthService) is responsible
        // for hashing and persisting it via IRefreshTokenHasher + IUnitOfWork.RefreshTokens.
        var randomBytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(randomBytes);
    }
}