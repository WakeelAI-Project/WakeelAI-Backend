using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Wakeel.Application.DTOs.Employees;
using Wakeel.Application.Interfaces;
using Wakeel.Application.Interfaces.Repositories;
using Wakeel.Application.Services;
using Wakeel.Domain.Entities;
using Xunit;

namespace Wakeel.Tests.Unit.Services;

public class EmployeeServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<IEmployeeProfileRepository> _employeeProfileRepositoryMock = new();
    private readonly Mock<IPasswordHasher> _passwordHasherMock = new();
    private readonly Mock<ILogger<EmployeeService>> _loggerMock = new();
    private readonly Mock<IEmailSender> _emailSenderMock = new();

    private readonly EmployeeService _sut;

    public EmployeeServiceTests()
    {
        _unitOfWorkMock.Setup(u => u.Users).Returns(_userRepositoryMock.Object);
        _unitOfWorkMock.Setup(u => u.EmployeeProfiles).Returns(_employeeProfileRepositoryMock.Object);

        _passwordHasherMock
            .Setup(h => h.HashPassword(It.IsAny<string>()))
            .Returns("hashed_temp_password");

        _sut = new EmployeeService(
            _unitOfWorkMock.Object,
            _passwordHasherMock.Object,
            _loggerMock.Object,
            _emailSenderMock.Object
        );
    }

    // ------------------------------------------------------------
    // CreateEmployeeAsync
    // ------------------------------------------------------------

    [Fact]
    public async Task CreateEmployeeAsync_GivenDuplicateEmail_ShouldThrowAndNotCreateAnything()
    {
        // Arrange
        var request = CreateValidRequest();

        _userRepositoryMock
            .Setup(r => r.EmailExistsAsync(request.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var act = () => _sut.CreateEmployeeAsync(Guid.NewGuid(), Guid.NewGuid(), request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("email_already_exists");

        _userRepositoryMock.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _employeeProfileRepositoryMock.Verify(r => r.AddAsync(It.IsAny<EmployeeProfile>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateEmployeeAsync_GivenHireDateInFuture_ShouldThrowAndNotCreateAnything()
    {
        // Arrange
        var request = CreateValidRequest() with { HireDate = DateTime.UtcNow.AddDays(1) };

        // Act
        var act = () => _sut.CreateEmployeeAsync(Guid.NewGuid(), Guid.NewGuid(), request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("hire_date_in_future");

        _userRepositoryMock.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _employeeProfileRepositoryMock.Verify(r => r.AddAsync(It.IsAny<EmployeeProfile>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateEmployeeAsync_GivenValidRequest_ShouldCreateUserAndProfileAndReturnActiveStatus()
    {
        // Arrange
        var actorUserId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var request = CreateValidRequest();

        _userRepositoryMock
            .Setup(r => r.EmailExistsAsync(request.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _sut.CreateEmployeeAsync(actorUserId, companyId, request);

        // Assert
        result.Should().NotBeNull();
        result.FullName.Should().Be(request.FullName);
        result.JobTitle.Should().Be(request.JobTitle);
        result.Salary.Should().Be(request.Salary);
        result.EmploymentStatus.Should().Be("Active");
        result.UserId.Should().Be(result.RecordId);

        _userRepositoryMock.Verify(r => r.AddAsync(
            It.Is<User>(u =>
                u.Email == request.Email &&
                u.FullName == request.FullName &&
                u.CompanyId == companyId &&
                u.CreatedByUserId == actorUserId &&
                u.IsActive),
            It.IsAny<CancellationToken>()), Times.Once);

        _employeeProfileRepositoryMock.Verify(r => r.AddAsync(
            It.Is<EmployeeProfile>(p =>
                p.JobTitle == request.JobTitle &&
                p.Salary == request.Salary &&
                p.ContractType == request.ContractType &&
                p.NationalId == null),
            It.IsAny<CancellationToken>()), Times.Once);

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _emailSenderMock.Verify(e => e.SendEmailAsync(request.Email, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateEmployeeAsync_GivenEmailSendFailure_ShouldStillSucceed()
    {
        // Arrange
        var request = CreateValidRequest();

        _userRepositoryMock
            .Setup(r => r.EmailExistsAsync(request.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _emailSenderMock
            .Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("smtp_unreachable"));

        // Act
        var result = await _sut.CreateEmployeeAsync(Guid.NewGuid(), Guid.NewGuid(), request);

        // Assert
        result.Should().NotBeNull();
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static CreateEmployeeRequest CreateValidRequest()
    {
        return new CreateEmployeeRequest
        {
            FullName = "Nour Hassan",
            Email = $"nour_{Guid.NewGuid():N}@test.com",
            JobTitle = "Software Engineer",
            HireDate = DateTime.UtcNow.AddDays(-1),
            Salary = 15000m,
            ContractType = "Full-Time"
        };
    }
}
