using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Wakeel.Application.DTOs.Auth;
using Wakeel.Application.Enums;
using Wakeel.Application.Interfaces;
using Wakeel.Application.Interfaces.Repositories;
using Wakeel.Application.Services;
using Wakeel.Domain.Entities;
using Wakeel.Domain.Enums;
using Xunit;

namespace Wakeel.Tests.Unit.Services;

public class AuthServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<ICompanyRepository> _companyRepositoryMock = new();
    private readonly Mock<IRefreshTokenRepository> _refreshTokenRepositoryMock = new();
    private readonly Mock<IPasswordHasher> _passwordHasherMock = new();
    private readonly Mock<IJwtTokenGenerator> _tokenGeneratorMock = new();
    private readonly Mock<IRefreshTokenHasher> _refreshTokenHasherMock = new();
    private readonly Mock<ILogger<AuthService>> _loggerMock = new();

    private readonly AuthService _sut; // "System Under Test"

    public AuthServiceTests()
    {
        _unitOfWorkMock.Setup(u => u.Users).Returns(_userRepositoryMock.Object);
        _unitOfWorkMock.Setup(u => u.Companies).Returns(_companyRepositoryMock.Object);
        _unitOfWorkMock.Setup(u => u.RefreshTokens).Returns(_refreshTokenRepositoryMock.Object);

        _tokenGeneratorMock.Setup(t => t.AccessTokenExpirationSeconds).Returns(900);
        _tokenGeneratorMock.Setup(t => t.RefreshTokenExpirationDays).Returns(7);

        _sut = new AuthService(
            _unitOfWorkMock.Object,
            _passwordHasherMock.Object,
            _tokenGeneratorMock.Object,
            _refreshTokenHasherMock.Object,
            _loggerMock.Object
        );
    }

    // ------------------------------------------------------------
    // RegisterCompanyAsync
    // ------------------------------------------------------------

    /// <summary>
    /// Tests the RegisterCompanyAsync method of the AuthService class when a duplicate email is provided.
    /// It verifies that the method returns the appropriate status and does not create a new company.
    /// </summary>
    /// <returns></returns>

    [Fact]
    public async Task RegisterCompanyAsync_GivenDuplicateEmail_ShouldReturnEmailAlreadyExists()
    {
        // Arrange
        var request = new RegisterCompanyRequest
        {
            CompanyName = "Test Corp",
            TaxId = "123456789",
            OwnerFullName = "Sara Ahmed",
            OwnerEmail = "sara@test.com",
            Password = "StrongPassword123!"
        };

        _userRepositoryMock
            .Setup(r => r.EmailExistsAsync(request.OwnerEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.RegisterCompanyAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(AuthResultStatus.EmailAlreadyExists);
        result.Data.Should().BeNull();

        _companyRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<Company>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "no company should be created when the email already exists"
        );
    }

    [Fact]
    public async Task RegisterCompanyAsync_GivenValidRequest_ShouldCreateCompanyAndOwnerUser()
    {
        // Arrange
        var request = new RegisterCompanyRequest
        {
            CompanyName = "Test Corp",
            TaxId = "123456789",
            OwnerFullName = "Sara Ahmed",
            OwnerEmail = "sara@test.com",
            Password = "StrongPassword123!"
        };

        _userRepositoryMock
            .Setup(r => r.EmailExistsAsync(request.OwnerEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _passwordHasherMock
            .Setup(h => h.HashPassword(request.Password))
            .Returns("hashed_password");

        _tokenGeneratorMock
            .Setup(t => t.GenerateAccessToken(It.IsAny<Guid>(), request.OwnerEmail, UserRole.Company_Owner, It.IsAny<Guid>()))
            .Returns("fake_access_token");

        _tokenGeneratorMock
            .Setup(t => t.GenerateRefreshToken(It.IsAny<Guid>()))
            .Returns("fake_refresh_token");

        _refreshTokenHasherMock
            .Setup(h => h.Hash("fake_refresh_token"))
            .Returns("hashed_refresh_token");

        // Act
        var result = await _sut.RegisterCompanyAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Status.Should().Be(AuthResultStatus.Success);
        result.Data.Should().NotBeNull();
        result.Data!.Role.Should().Be(nameof(UserRole.Company_Owner));
        result.Data.AccessToken.Should().Be("fake_access_token");
        result.Data.RefreshToken.Should().Be("fake_refresh_token");

        _companyRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Company>(), It.IsAny<CancellationToken>()), Times.Once);
        _userRepositoryMock.Verify(r => r.AddAsync(It.Is<User>(u => u.Role == UserRole.Company_Owner), It.IsAny<CancellationToken>()), Times.Once);
        _refreshTokenRepositoryMock.Verify(r => r.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    // ------------------------------------------------------------
    // LoginAsync
    // ------------------------------------------------------------

    [Fact]
    public async Task LoginAsync_GivenNonExistentEmail_ShouldReturnInvalidCredentials()
    {
        // Arrange
        var request = new LoginRequest { Email = "nobody@test.com", Password = "whatever" };

        _userRepositoryMock
            .Setup(r => r.GetByEmailAsync(request.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _sut.LoginAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(AuthResultStatus.InvalidCredentials);
    }

    [Fact]
    public async Task LoginAsync_GivenWrongPassword_ShouldReturnInvalidCredentials()
    {
        // Arrange
        var user = CreateTestUser();
        var request = new LoginRequest { Email = user.Email, Password = "wrong_password" };

        _userRepositoryMock
            .Setup(r => r.GetByEmailAsync(request.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _passwordHasherMock
            .Setup(h => h.VerifyPassword(request.Password, user.PasswordHash))
            .Returns(false);

        // Act
        var result = await _sut.LoginAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(AuthResultStatus.InvalidCredentials);
    }

    [Fact]
    public async Task LoginAsync_GivenInactiveAccount_ShouldReturnAccountInactive()
    {
        // Arrange
        var user = CreateTestUser(isActive: false);
        var request = new LoginRequest { Email = user.Email, Password = "correct_password" };

        _userRepositoryMock
            .Setup(r => r.GetByEmailAsync(request.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _passwordHasherMock
            .Setup(h => h.VerifyPassword(request.Password, user.PasswordHash))
            .Returns(true);

        // Act
        var result = await _sut.LoginAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(AuthResultStatus.AccountInactive);
    }

    [Fact]
    public async Task LoginAsync_GivenValidCredentials_ShouldReturnTokens()
    {
        // Arrange
        var user = CreateTestUser();
        var request = new LoginRequest { Email = user.Email, Password = "correct_password" };

        _userRepositoryMock
            .Setup(r => r.GetByEmailAsync(request.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _passwordHasherMock
            .Setup(h => h.VerifyPassword(request.Password, user.PasswordHash))
            .Returns(true);

        _tokenGeneratorMock
            .Setup(t => t.GenerateAccessToken(user.Id, user.Email, user.Role, user.CompanyId))
            .Returns("fake_access_token");

        _tokenGeneratorMock
            .Setup(t => t.GenerateRefreshToken(user.Id))
            .Returns("fake_refresh_token");

        // Act
        var result = await _sut.LoginAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.AccessToken.Should().Be("fake_access_token");
        result.Data.ExpiresIn.Should().Be(900);
        result.Data.MustChangePassword.Should().BeFalse();
    }

    [Fact]
    public async Task LoginAsync_GivenUserWithMustChangePassword_ShouldReturnFlagTrue()
    {
        // Arrange
        var user = CreateTestUser(mustChangePassword: true);
        var request = new LoginRequest { Email = user.Email, Password = "correct_password" };

        _userRepositoryMock
            .Setup(r => r.GetByEmailAsync(request.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _passwordHasherMock
            .Setup(h => h.VerifyPassword(request.Password, user.PasswordHash))
            .Returns(true);

        _tokenGeneratorMock
            .Setup(t => t.GenerateAccessToken(user.Id, user.Email, user.Role, user.CompanyId))
            .Returns("fake_access_token");

        _tokenGeneratorMock
            .Setup(t => t.GenerateRefreshToken(user.Id))
            .Returns("fake_refresh_token");

        // Act
        var result = await _sut.LoginAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.MustChangePassword.Should().BeTrue();
    }

    // ------------------------------------------------------------
    // ChangePasswordAsync
    // ------------------------------------------------------------

    [Fact]
    public async Task ChangePasswordAsync_GivenUnknownUser_ShouldReturnUserNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new ChangePasswordRequest { CurrentPassword = "temp_password", NewPassword = "NewStrongPassword123!" };

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var (isSuccess, errorMessage) = await _sut.ChangePasswordAsync(userId, request);

        // Assert
        isSuccess.Should().BeFalse();
        errorMessage.Should().Be("user_not_found");
    }

    [Fact]
    public async Task ChangePasswordAsync_GivenWrongCurrentPassword_ShouldReturnInvalidCurrentPassword()
    {
        // Arrange
        var user = CreateTestUser(mustChangePassword: true);
        var request = new ChangePasswordRequest { CurrentPassword = "wrong_temp_password", NewPassword = "NewStrongPassword123!" };

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _passwordHasherMock
            .Setup(h => h.VerifyPassword(request.CurrentPassword, user.PasswordHash))
            .Returns(false);

        // Act
        var (isSuccess, errorMessage) = await _sut.ChangePasswordAsync(user.Id, request);

        // Assert
        isSuccess.Should().BeFalse();
        errorMessage.Should().Be("invalid_current_password");
        user.MustChangePassword.Should().BeTrue("a failed change must not clear the flag");
    }

    [Fact]
    public async Task ChangePasswordAsync_GivenValidRequest_ShouldClearMustChangePasswordFlag()
    {
        // Arrange
        var user = CreateTestUser(mustChangePassword: true);
        var request = new ChangePasswordRequest { CurrentPassword = "temp_password", NewPassword = "NewStrongPassword123!" };

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _passwordHasherMock
            .Setup(h => h.VerifyPassword(request.CurrentPassword, user.PasswordHash))
            .Returns(true);

        _passwordHasherMock
            .Setup(h => h.HashPassword(request.NewPassword))
            .Returns("new_hashed_password");

        // Act
        var (isSuccess, errorMessage) = await _sut.ChangePasswordAsync(user.Id, request);

        // Assert
        isSuccess.Should().BeTrue();
        errorMessage.Should().BeNull();
        user.MustChangePassword.Should().BeFalse();
        user.PasswordHash.Should().Be("new_hashed_password");
        _userRepositoryMock.Verify(r => r.Update(user), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ------------------------------------------------------------
    // RefreshTokenAsync
    // ------------------------------------------------------------

    [Fact]
    public async Task RefreshTokenAsync_GivenRevokedToken_ShouldReturnInvalidRefreshToken()
    {
        // Arrange
        var request = new RefreshTokenRequest { RefreshToken = "raw_token" };
        var storedToken = new RefreshToken { UserId = Guid.NewGuid(), IsRevoked = true, ExpiresAt = DateTime.UtcNow.AddDays(1) };

        _refreshTokenHasherMock.Setup(h => h.Hash(request.RefreshToken)).Returns("hashed_token");
        _refreshTokenRepositoryMock
            .Setup(r => r.GetByTokenHashAsync("hashed_token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(storedToken);

        // Act
        var result = await _sut.RefreshTokenAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(AuthResultStatus.InvalidRefreshToken);
    }

    [Fact]
    public async Task RefreshTokenAsync_GivenExpiredToken_ShouldReturnRefreshTokenExpired()
    {
        // Arrange
        var request = new RefreshTokenRequest { RefreshToken = "raw_token" };
        var storedToken = new RefreshToken { UserId = Guid.NewGuid(), IsRevoked = false, ExpiresAt = DateTime.UtcNow.AddDays(-1) };

        _refreshTokenHasherMock.Setup(h => h.Hash(request.RefreshToken)).Returns("hashed_token");
        _refreshTokenRepositoryMock
            .Setup(r => r.GetByTokenHashAsync("hashed_token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(storedToken);

        // Act
        var result = await _sut.RefreshTokenAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(AuthResultStatus.RefreshTokenExpired);
    }

    [Fact]
    public async Task RefreshTokenAsync_GivenValidToken_ShouldRevokeOldTokenAndReturnNewTokens()
    {
        // Arrange
        var user = CreateTestUser();
        var request = new RefreshTokenRequest { RefreshToken = "raw_old_token" };
        var storedToken = new RefreshToken { UserId = user.Id, IsRevoked = false, ExpiresAt = DateTime.UtcNow.AddDays(1) };

        _refreshTokenHasherMock.Setup(h => h.Hash(request.RefreshToken)).Returns("hashed_old_token");
        _refreshTokenRepositoryMock
            .Setup(r => r.GetByTokenHashAsync("hashed_old_token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(storedToken);

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _tokenGeneratorMock
            .Setup(t => t.GenerateAccessToken(user.Id, user.Email, user.Role, user.CompanyId))
            .Returns("new_access_token");

        _tokenGeneratorMock
            .Setup(t => t.GenerateRefreshToken(user.Id))
            .Returns("new_raw_refresh_token");

        _refreshTokenHasherMock
            .Setup(h => h.Hash("new_raw_refresh_token"))
            .Returns("hashed_new_token");

        // Act
        var result = await _sut.RefreshTokenAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.AccessToken.Should().Be("new_access_token");
        result.Data.RefreshToken.Should().Be("new_raw_refresh_token");
        result.Data.ExpiresIn.Should().Be(900);

        // The old token must be revoked
        storedToken.IsRevoked.Should().BeTrue();
        _refreshTokenRepositoryMock.Verify(r => r.Update(storedToken), Times.Once);

        // A brand new refresh token row must be persisted
        _refreshTokenRepositoryMock.Verify(
            r => r.AddAsync(It.Is<RefreshToken>(rt => rt.UserId == user.Id), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    // ------------------------------------------------------------
    // LogoutAsync
    // ------------------------------------------------------------

    [Fact]
    public async Task LogoutAsync_GivenValidToken_ShouldRevokeIt()
    {
        // Arrange
        var request = new LogoutRequest { RefreshToken = "raw_token" };
        var storedToken = new RefreshToken { UserId = Guid.NewGuid(), IsRevoked = false };

        _refreshTokenHasherMock.Setup(h => h.Hash(request.RefreshToken)).Returns("hashed_token");
        _refreshTokenRepositoryMock
            .Setup(r => r.GetByTokenHashAsync("hashed_token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(storedToken);

        // Act
        var result = await _sut.LogoutAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        storedToken.IsRevoked.Should().BeTrue();
        _refreshTokenRepositoryMock.Verify(r => r.Update(storedToken), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LogoutAsync_GivenNonExistentToken_ShouldStillReturnSuccess()
    {
        // Arrange — logout is idempotent: unknown token shouldn't reveal anything or error
        var request = new LogoutRequest { RefreshToken = "unknown_token" };

        _refreshTokenHasherMock.Setup(h => h.Hash(request.RefreshToken)).Returns("hashed_token");
        _refreshTokenRepositoryMock
            .Setup(r => r.GetByTokenHashAsync("hashed_token", It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefreshToken?)null);

        // Act
        var result = await _sut.LogoutAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _refreshTokenRepositoryMock.Verify(r => r.Update(It.IsAny<RefreshToken>()), Times.Never);
    }

    // ------------------------------------------------------------
    // Test data helper
    // ------------------------------------------------------------

    private static User CreateTestUser(bool isActive = true, bool mustChangePassword = false)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            CompanyId = Guid.NewGuid(),
            Email = "sara@test.com",
            PasswordHash = "hashed_correct_password",
            FullName = "Sara Ahmed",
            Role = UserRole.Company_Owner,
            IsActive = isActive,
            MustChangePassword = mustChangePassword,
            CreatedAt = DateTime.UtcNow
        };
    }
}