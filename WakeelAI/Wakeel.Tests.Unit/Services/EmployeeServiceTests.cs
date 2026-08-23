using System;
using System.Collections.Generic;
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
    private readonly Mock<IRefreshTokenRepository> _refreshTokenRepositoryMock = new();
    private readonly Mock<ILeaveBalanceRepository> _leaveBalanceRepositoryMock = new();
    private readonly Mock<ILeaveRequestRepository> _leaveRequestRepositoryMock = new();
    private readonly Mock<IDepartmentRepository> _departmentRepositoryMock = new();
    private readonly Mock<ILeaveEntitlementRepository> _leaveEntitlementRepositoryMock = new();
    private readonly Mock<IPasswordHasher> _passwordHasherMock = new();
    private readonly Mock<ILogger<EmployeeService>> _loggerMock = new();
    private readonly Mock<IEmailSender> _emailSenderMock = new();
    private readonly Mock<IResourceLoader> _resourceLoaderMock = new();
    private readonly Mock<IAuditLogService> _auditLogServiceMock = new();

    private readonly EmployeeService _sut;

    public EmployeeServiceTests()
    {
        _unitOfWorkMock.Setup(u => u.Users).Returns(_userRepositoryMock.Object);
        _unitOfWorkMock.Setup(u => u.EmployeeProfiles).Returns(_employeeProfileRepositoryMock.Object);
        _unitOfWorkMock.Setup(u => u.RefreshTokens).Returns(_refreshTokenRepositoryMock.Object);
        _unitOfWorkMock.Setup(u => u.LeaveBalances).Returns(_leaveBalanceRepositoryMock.Object);
        _unitOfWorkMock.Setup(u => u.LeaveRequests).Returns(_leaveRequestRepositoryMock.Object);
        _unitOfWorkMock.Setup(u => u.Departments).Returns(_departmentRepositoryMock.Object);
        _unitOfWorkMock.Setup(u => u.LeaveEntitlements).Returns(_leaveEntitlementRepositoryMock.Object);

        // Real seeded entitlement rows (mirrors LeaveEntitlementConfiguration) so the real
        // LeaveBalanceProvisioningService below computes genuine tiered values, not stubs.
        _leaveEntitlementRepositoryMock
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<LeaveEntitlement, bool>>>(), It.IsAny<CancellationToken>()))
            .Returns<System.Linq.Expressions.Expression<Func<LeaveEntitlement, bool>>, CancellationToken>((predicate, _) =>
                Task.FromResult(SeededEntitlements.FirstOrDefault(predicate.Compile())));

        _refreshTokenRepositoryMock
            .Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<RefreshToken, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RefreshToken>());

        _departmentRepositoryMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Department>());

        _leaveBalanceRepositoryMock
            .Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<LeaveBalance, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LeaveBalance>());

        _leaveRequestRepositoryMock
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<LeaveRequest, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveRequest?)null);

        _passwordHasherMock
            .Setup(h => h.HashPassword(It.IsAny<string>()))
            .Returns("hashed_temp_password");
        _resourceLoaderMock
            .Setup(r => r.GetEmployeeProfileAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns<Guid, CancellationToken>((id, ct) => _employeeProfileRepositoryMock.Object.GetByIdAsync(id, ct));

        _resourceLoaderMock
            .Setup(r => r.GetUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns<Guid, CancellationToken>((id, ct) => _userRepositoryMock.Object.GetByIdAsync(id, ct));

        // LeaveBalanceProvisioningService resolves an employee's HireDate via
        // EmployeeProfiles.GetByUserIdAsync. Delegate it to GetByIdAsync (EmployeeProfile's
        // primary key IS UserId) so any per-test GetByIdAsync setup - including one
        // registered after this constructor runs - is honoured automatically. A profile
        // added via AddAsync during CreateEmployeeAsync (and not yet given its own
        // GetByIdAsync setup) is captured here too, so it becomes visible in the same test.
        _employeeProfileRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<EmployeeProfile>(), It.IsAny<CancellationToken>()))
            .Callback<EmployeeProfile, CancellationToken>((p, _) => _addedProfiles[p.UserId] = p)
            .Returns(Task.CompletedTask);
        _employeeProfileRepositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns<Guid, CancellationToken>((id, _) => Task.FromResult(_addedProfiles.GetValueOrDefault(id)));
        _employeeProfileRepositoryMock
            .Setup(r => r.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns<Guid, CancellationToken>((id, ct) => _employeeProfileRepositoryMock.Object.GetByIdAsync(id, ct));

        var leaveBalanceProvisioningService = new LeaveBalanceProvisioningService(_unitOfWorkMock.Object);

        _sut = new EmployeeService(
            _unitOfWorkMock.Object,
            _passwordHasherMock.Object,
            _loggerMock.Object,
            _emailSenderMock.Object,
            _resourceLoaderMock.Object,
            leaveBalanceProvisioningService,
            _auditLogServiceMock.Object
        );
    }

    private readonly Dictionary<Guid, EmployeeProfile> _addedProfiles = new();

    /// <summary>Mirrors the seed data in LeaveEntitlementConfiguration.</summary>
    private static readonly List<LeaveEntitlement> SeededEntitlements = new()
    {
        new LeaveEntitlement { LeaveType = "Annual", BaseDays = 15, StandardDays = 21, SeniorDays = 30, SeniorityYears = 10, MinimumServiceMonths = 6 },
        new LeaveEntitlement { LeaveType = "Sick", DefaultDays = null },
        new LeaveEntitlement { LeaveType = "Unpaid", DefaultDays = null }
    };

    // ------------------------------------------------------------
    // CreateEmployeeAsync
    // ------------------------------------------------------------

    [Fact]
    public async Task CreateEmployeeAsync_GivenDuplicateEmail_ShouldThrowAndNotCreateAnything()
    {
        // Arrange
        var companyId = Guid.NewGuid();
        var request = CreateValidRequest();
        SetupValidDepartment(companyId, request.DepartmentId!.Value);

        _userRepositoryMock
            .Setup(r => r.EmailExistsAsync(request.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var act = () => _sut.CreateEmployeeAsync(Guid.NewGuid(), companyId, request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("email_already_exists");

        _userRepositoryMock.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _employeeProfileRepositoryMock.Verify(r => r.AddAsync(It.IsAny<EmployeeProfile>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateEmployeeAsync_GivenUnknownDepartment_ShouldThrowAndNotCreateAnything()
    {
        // Arrange
        var request = CreateValidRequest();
        _departmentRepositoryMock
            .Setup(r => r.GetByIdAsync(request.DepartmentId!.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Department?)null);

        // Act
        var act = () => _sut.CreateEmployeeAsync(Guid.NewGuid(), Guid.NewGuid(), request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("department_not_found");

        _userRepositoryMock.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _employeeProfileRepositoryMock.Verify(r => r.AddAsync(It.IsAny<EmployeeProfile>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateEmployeeAsync_GivenDepartmentFromAnotherCompany_ShouldThrowAndNotCreateAnything()
    {
        // Arrange
        var request = CreateValidRequest();
        _departmentRepositoryMock
            .Setup(r => r.GetByIdAsync(request.DepartmentId!.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Department { Id = request.DepartmentId!.Value, CompanyId = Guid.NewGuid(), IsDeleted = false });

        // Act
        var act = () => _sut.CreateEmployeeAsync(Guid.NewGuid(), Guid.NewGuid(), request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("department_not_found");
        _employeeProfileRepositoryMock.Verify(r => r.AddAsync(It.IsAny<EmployeeProfile>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateEmployeeAsync_GivenSoftDeletedDepartment_ShouldThrowAndNotCreateAnything()
    {
        // Arrange
        var companyId = Guid.NewGuid();
        var request = CreateValidRequest();
        _departmentRepositoryMock
            .Setup(r => r.GetByIdAsync(request.DepartmentId!.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Department { Id = request.DepartmentId!.Value, CompanyId = companyId, IsDeleted = true });

        // Act
        var act = () => _sut.CreateEmployeeAsync(Guid.NewGuid(), companyId, request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("department_not_found");
        _employeeProfileRepositoryMock.Verify(r => r.AddAsync(It.IsAny<EmployeeProfile>(), It.IsAny<CancellationToken>()), Times.Never);
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
        SetupValidDepartment(companyId, request.DepartmentId!.Value);

        _userRepositoryMock
            .Setup(r => r.EmailExistsAsync(request.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _sut.CreateEmployeeAsync(actorUserId, companyId, request);

        // Assert
        result.Should().NotBeNull();
        result.FullName.Should().Be(request.FullName);
        result.JobTitle.Should().Be(request.JobTitle);
        result.DepartmentId.Should().Be(request.DepartmentId!.Value);
        result.Salary.Should().Be(request.Salary);
        result.EmploymentStatus.Should().Be("Active");
        result.UserId.Should().Be(result.RecordId);

        _userRepositoryMock.Verify(r => r.AddAsync(
            It.Is<User>(u =>
                u.Email == request.Email &&
                u.FullName == request.FullName &&
                u.CompanyId == companyId &&
                u.CreatedByUserId == actorUserId &&
                u.IsActive &&
                u.MustChangePassword),
            It.IsAny<CancellationToken>()), Times.Once);

        _employeeProfileRepositoryMock.Verify(r => r.AddAsync(
            It.Is<EmployeeProfile>(p =>
                p.JobTitle == request.JobTitle &&
                p.Salary == request.Salary &&
                p.ContractType == request.ContractType &&
                p.DepartmentId == request.DepartmentId &&
                p.NationalId == null),
            It.IsAny<CancellationToken>()), Times.Once);

        _leaveBalanceRepositoryMock.Verify(r => r.AddAsync(It.IsAny<LeaveBalance>(), It.IsAny<CancellationToken>()), Times.Exactly(3));

        // FIX-01: CreateEmployeeAsync now saves twice - once so the just-added profile is
        // visible to the provisioning service's own EmployeeProfiles lookup (which only
        // sees persisted rows), then again to persist the provisioned leave balances.
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        _emailSenderMock.Verify(e => e.SendEmailAsync(request.Email, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateEmployeeAsync_GivenValidRequest_ShouldInitializeLeaveBalancesForHireYear()
    {
        // Arrange
        var companyId = Guid.NewGuid();
        // A fixed historical hire date, not "yesterday": the Annual assertion below needs
        // "under 6 months of service by the hire year's own year-end" to hold no matter
        // which calendar month this test happens to run in.
        var request = CreateValidRequest() with { HireDate = new DateTime(2020, 11, 15) };
        SetupValidDepartment(companyId, request.DepartmentId!.Value);
        var hireYear = request.HireDate.Year;
        var addedBalances = new List<LeaveBalance>();

        _userRepositoryMock
            .Setup(r => r.EmailExistsAsync(request.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _leaveBalanceRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<LeaveBalance>(), It.IsAny<CancellationToken>()))
            .Callback<LeaveBalance, CancellationToken>((lb, _) => addedBalances.Add(lb))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.CreateEmployeeAsync(Guid.NewGuid(), companyId, request);

        // Assert
        addedBalances.Should().HaveCount(3);
        addedBalances.Should().OnlyContain(lb => lb.EmployeeId == result.RecordId && lb.Year == hireYear && lb.UsedDays == 0);

        // Hired "yesterday": always under 6 months of service by the hire year's own
        // year-end, so Annual reads 0 for this, its very first, partial year - it can
        // never reach the prorated/standard/senior tiers within the hire year itself.
        // Exact tier boundaries are covered by LeaveBalanceProvisioningServiceTests with
        // fixed, calendar-independent dates.
        var annual = addedBalances.Should().ContainSingle(lb => lb.LeaveType == "Annual").Subject;
        annual.TotalDays.Should().Be(0);

        var sick = addedBalances.Should().ContainSingle(lb => lb.LeaveType == "Sick").Subject;
        sick.TotalDays.Should().BeNull("Sick has no fixed day quota under Law No. 14/2025");

        var unpaid = addedBalances.Should().ContainSingle(lb => lb.LeaveType == "Unpaid").Subject;
        unpaid.TotalDays.Should().BeNull("Unpaid has no statutory quota");
    }

    [Fact]
    public async Task CreateEmployeeAsync_GivenEmailSendFailure_ShouldStillSucceed()
    {
        // Arrange
        var companyId = Guid.NewGuid();
        var request = CreateValidRequest();
        SetupValidDepartment(companyId, request.DepartmentId!.Value);

        _userRepositoryMock
            .Setup(r => r.EmailExistsAsync(request.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _emailSenderMock
            .Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("smtp_unreachable"));

        // Act
        var result = await _sut.CreateEmployeeAsync(Guid.NewGuid(), companyId, request);

        // Assert
        result.Should().NotBeNull();
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    // ------------------------------------------------------------
    // GetEmployeeAsync
    // ------------------------------------------------------------

    [Fact]
    public async Task GetEmployeeAsync_GivenUnknownRecordId_ShouldReturnNull()
    {
        // Arrange
        _employeeProfileRepositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmployeeProfile?)null);

        // Act
        var result = await _sut.GetEmployeeAsync(Guid.NewGuid(), Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetEmployeeAsync_GivenRecordFromAnotherCompany_ShouldReturnNull()
    {
        // Arrange
        var (profile, user) = CreateProfileAndUser();

        _employeeProfileRepositoryMock.Setup(r => r.GetByIdAsync(profile.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        _userRepositoryMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        // Act
        var result = await _sut.GetEmployeeAsync(Guid.NewGuid(), profile.UserId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetEmployeeAsync_GivenValidRecord_ShouldReturnFullDetail()
    {
        // Arrange
        var (profile, user) = CreateProfileAndUser();
        profile.NationalId = "29001011234567";

        _employeeProfileRepositoryMock.Setup(r => r.GetByIdAsync(profile.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        _userRepositoryMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        // Act
        var result = await _sut.GetEmployeeAsync(user.CompanyId, profile.UserId);

        // Assert
        result.Should().NotBeNull();
        result!.RecordId.Should().Be(profile.UserId);
        result.UserId.Should().Be(user.Id);
        result.Email.Should().Be(user.Email);
        result.JobTitle.Should().Be(profile.JobTitle);
        result.DepartmentId.Should().Be(profile.DepartmentId);
        result.NationalId.Should().Be("29001011234567");
        result.EmploymentStatus.Should().Be("Active");
    }

    [Fact]
    public async Task GetEmployeeAsync_ShouldIncludeDepartmentName()
    {
        // Arrange
        var (profile, user) = CreateProfileAndUser();
        var department = new Department { Id = profile.DepartmentId, CompanyId = user.CompanyId, Name = "Engineering", IsDeleted = false };

        _employeeProfileRepositoryMock.Setup(r => r.GetByIdAsync(profile.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        _userRepositoryMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _departmentRepositoryMock.Setup(r => r.GetByIdAsync(profile.DepartmentId, It.IsAny<CancellationToken>())).ReturnsAsync(department);

        // Act
        var result = await _sut.GetEmployeeAsync(user.CompanyId, profile.UserId);

        // Assert
        result!.Department.Should().Be("Engineering");
    }

    [Fact]
    public async Task GetEmployeeAsync_ShouldMapCurrentYearLeaveBalances()
    {
        // Arrange
        var (profile, user) = CreateProfileAndUser();
        var currentYear = DateTime.UtcNow.Year;

        _employeeProfileRepositoryMock.Setup(r => r.GetByIdAsync(profile.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        _userRepositoryMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var balances = new List<LeaveBalance>
        {
            new() { Id = Guid.NewGuid(), EmployeeId = profile.UserId, LeaveType = "Annual", TotalDays = 15, UsedDays = 2, Year = currentYear },
            new() { Id = Guid.NewGuid(), EmployeeId = profile.UserId, LeaveType = "Sick", TotalDays = 10, UsedDays = 0, Year = currentYear },
            new() { Id = Guid.NewGuid(), EmployeeId = profile.UserId, LeaveType = "Unpaid", TotalDays = null, UsedDays = 1, Year = currentYear }
        };
        _leaveBalanceRepositoryMock
            .Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<LeaveBalance, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(balances);

        // Act
        var result = await _sut.GetEmployeeAsync(user.CompanyId, profile.UserId);

        // Assert
        result!.LeaveBalance.Should().NotBeNull();
        result.LeaveBalance!.Annual.Should().BeEquivalentTo(new LeaveTypeBalance { TotalDays = 15, UsedDays = 2, RemainingDays = 13 });
        result.LeaveBalance.Sick.Should().BeEquivalentTo(new LeaveTypeBalance { TotalDays = 10, UsedDays = 0, RemainingDays = 10 });
        result.LeaveBalance.Unpaid.Should().BeEquivalentTo(new LeaveTypeBalance { TotalDays = null, UsedDays = 1, RemainingDays = null });
    }

    [Fact]
    public async Task GetEmployeeAsync_GivenNoLeaveInProgress_ShouldReturnNullCurrentLeave()
    {
        // Arrange
        var (profile, user) = CreateProfileAndUser();
        _employeeProfileRepositoryMock.Setup(r => r.GetByIdAsync(profile.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        _userRepositoryMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        // Act
        var result = await _sut.GetEmployeeAsync(user.CompanyId, profile.UserId);

        // Assert
        result!.CurrentLeave.Should().BeNull();
    }

    [Fact]
    public async Task GetEmployeeAsync_GivenApprovedLeaveInProgress_ShouldMapElapsedDaysFromToday()
    {
        // Arrange
        var (profile, user) = CreateProfileAndUser();
        _employeeProfileRepositoryMock.Setup(r => r.GetByIdAsync(profile.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        _userRepositoryMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var activeLeave = new LeaveRequest
        {
            Id = Guid.NewGuid(),
            EmployeeId = profile.UserId,
            CompanyId = user.CompanyId,
            LeaveType = "Annual",
            StartDate = today.AddDays(-2), // day 3 of a 5-day leave
            EndDate = today.AddDays(2),
            DaysRequested = 5,
            Status = "Approved"
        };
        _leaveRequestRepositoryMock
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<LeaveRequest, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeLeave);

        // Act
        var result = await _sut.GetEmployeeAsync(user.CompanyId, profile.UserId);

        // Assert
        result!.CurrentLeave.Should().BeEquivalentTo(new CurrentLeaveInfo
        {
            LeaveType = "Annual",
            StartDate = activeLeave.StartDate.ToString("yyyy-MM-dd"),
            EndDate = activeLeave.EndDate.ToString("yyyy-MM-dd"),
            TotalDays = 5,
            ElapsedDays = 3
        });
    }

    [Fact]
    public async Task GetEmployeeAsync_GivenEmployeeTimeZone_ShouldComputeElapsedDaysUsingThatZonesLocalDate()
    {
        // Arrange
        var (profile, user) = CreateProfileAndUser();
        // UTC+14 — the earliest time zone on Earth, so its local date differs
        // from UTC's for most of every UTC day, making this reliably exercise
        // the zone-aware path rather than coincidentally matching UTC.
        profile.TimeZoneId = "Pacific/Kiritimati";

        _employeeProfileRepositoryMock.Setup(r => r.GetByIdAsync(profile.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        _userRepositoryMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(profile.TimeZoneId);
        var localToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone));

        var activeLeave = new LeaveRequest
        {
            Id = Guid.NewGuid(),
            EmployeeId = profile.UserId,
            CompanyId = user.CompanyId,
            LeaveType = "Annual",
            StartDate = localToday.AddDays(-2), // day 3 of a 5-day leave, in the employee's local date
            EndDate = localToday.AddDays(2),
            DaysRequested = 5,
            Status = "Approved"
        };
        _leaveRequestRepositoryMock
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<LeaveRequest, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeLeave);

        // Act
        var result = await _sut.GetEmployeeAsync(user.CompanyId, profile.UserId);

        // Assert
        result!.CurrentLeave.Should().NotBeNull();
        result.CurrentLeave!.ElapsedDays.Should().Be(3);
    }

    [Fact]
    public async Task GetEmployeeAsync_GivenInvalidStoredTimeZone_ShouldFallBackToUtcRatherThanThrow()
    {
        // Arrange — defends against corrupted/manually-edited data (an
        // invalid IANA id here must never break the whole profile fetch).
        var (profile, user) = CreateProfileAndUser();
        profile.TimeZoneId = "Not/A/Real/Zone";

        _employeeProfileRepositoryMock.Setup(r => r.GetByIdAsync(profile.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        _userRepositoryMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var utcToday = DateOnly.FromDateTime(DateTime.UtcNow);
        var activeLeave = new LeaveRequest
        {
            Id = Guid.NewGuid(),
            EmployeeId = profile.UserId,
            CompanyId = user.CompanyId,
            LeaveType = "Annual",
            StartDate = utcToday.AddDays(-1),
            EndDate = utcToday.AddDays(1),
            DaysRequested = 3,
            Status = "Approved"
        };
        _leaveRequestRepositoryMock
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<LeaveRequest, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeLeave);

        // Act
        var result = await _sut.GetEmployeeAsync(user.CompanyId, profile.UserId);

        // Assert
        result.Should().NotBeNull();
        result!.CurrentLeave!.ElapsedDays.Should().Be(2);
    }

    // ------------------------------------------------------------
    // UpdateTimeZoneAsync
    // ------------------------------------------------------------

    [Fact]
    public async Task UpdateTimeZoneAsync_GivenValidIanaId_ShouldPersistAndReturnUpdatedDetail()
    {
        // Arrange
        var (profile, user) = CreateProfileAndUser();
        _employeeProfileRepositoryMock.Setup(r => r.GetByIdAsync(profile.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        _userRepositoryMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        // Act
        var result = await _sut.UpdateTimeZoneAsync(user.CompanyId, user.Id, "Africa/Cairo");

        // Assert
        result.Should().NotBeNull();
        profile.TimeZoneId.Should().Be("Africa/Cairo");
        _employeeProfileRepositoryMock.Verify(r => r.Update(profile), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateTimeZoneAsync_GivenInvalidTimeZoneId_ShouldThrowAndNotPersist()
    {
        // Arrange
        var (profile, user) = CreateProfileAndUser();
        _employeeProfileRepositoryMock.Setup(r => r.GetByIdAsync(profile.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        _userRepositoryMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        // Act
        var act = () => _sut.UpdateTimeZoneAsync(user.CompanyId, user.Id, "Not/A/Real/Zone");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("invalid_timezone");
        profile.TimeZoneId.Should().BeNull();
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateTimeZoneAsync_GivenUnknownUser_ShouldReturnNull()
    {
        // Arrange
        _employeeProfileRepositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmployeeProfile?)null);

        // Act
        var result = await _sut.UpdateTimeZoneAsync(Guid.NewGuid(), Guid.NewGuid(), "Africa/Cairo");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateTimeZoneAsync_GivenUserFromAnotherCompany_ShouldReturnNullAndNotPersist()
    {
        // Arrange
        var (profile, user) = CreateProfileAndUser();
        _employeeProfileRepositoryMock.Setup(r => r.GetByIdAsync(profile.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        _userRepositoryMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        // Act
        var result = await _sut.UpdateTimeZoneAsync(Guid.NewGuid(), user.Id, "Africa/Cairo");

        // Assert
        result.Should().BeNull();
        profile.TimeZoneId.Should().BeNull();
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ------------------------------------------------------------
    // UpdatePhotoAsync / RemovePhotoAsync
    // ------------------------------------------------------------

    [Fact]
    public async Task UpdatePhotoAsync_GivenUnknownUserId_ShouldReturnNull()
    {
        // Arrange
        _userRepositoryMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        // Act
        var result = await _sut.UpdatePhotoAsync(Guid.NewGuid(), Guid.NewGuid(), "/uploads/profile-photos/x.jpg");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdatePhotoAsync_GivenUserFromAnotherCompany_ShouldReturnNullAndNotSave()
    {
        // Arrange
        var (_, user) = CreateProfileAndUser();
        _userRepositoryMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        // Act
        var result = await _sut.UpdatePhotoAsync(Guid.NewGuid(), user.Id, "/uploads/profile-photos/x.jpg");

        // Assert
        result.Should().BeNull();
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdatePhotoAsync_GivenValidUser_ShouldSetPhotoUrlAndReturnUpdatedDetail()
    {
        // Arrange
        var (profile, user) = CreateProfileAndUser();
        _employeeProfileRepositoryMock.Setup(r => r.GetByIdAsync(profile.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        _userRepositoryMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        // Act
        var result = await _sut.UpdatePhotoAsync(user.CompanyId, user.Id, "/uploads/profile-photos/new.jpg");

        // Assert
        user.PhotoUrl.Should().Be("/uploads/profile-photos/new.jpg");
        result!.PhotoUrl.Should().Be("/uploads/profile-photos/new.jpg");
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemovePhotoAsync_GivenValidUser_ShouldClearPhotoUrl()
    {
        // Arrange
        var (profile, user) = CreateProfileAndUser();
        user.PhotoUrl = "/uploads/profile-photos/old.jpg";
        _employeeProfileRepositoryMock.Setup(r => r.GetByIdAsync(profile.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        _userRepositoryMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        // Act
        var result = await _sut.RemovePhotoAsync(user.CompanyId, user.Id);

        // Assert
        user.PhotoUrl.Should().BeNull();
        result!.PhotoUrl.Should().BeNull();
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemovePhotoAsync_GivenUserFromAnotherCompany_ShouldReturnNullAndNotSave()
    {
        // Arrange
        var (_, user) = CreateProfileAndUser();
        user.PhotoUrl = "/uploads/profile-photos/old.jpg";
        _userRepositoryMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        // Act
        var result = await _sut.RemovePhotoAsync(Guid.NewGuid(), user.Id);

        // Assert
        result.Should().BeNull();
        user.PhotoUrl.Should().Be("/uploads/profile-photos/old.jpg");
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ------------------------------------------------------------
    // ListEmployeesAsync
    // ------------------------------------------------------------

    [Fact]
    public async Task ListEmployeesAsync_ShouldOnlyReturnEmployeesOfCallerCompany()
    {
        // Arrange
        var companyId = Guid.NewGuid();
        var (profileA, userA) = CreateProfileAndUser();
        userA.CompanyId = companyId;
        var (profileB, userB) = CreateProfileAndUser(); // different company

        _employeeProfileRepositoryMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { profileA, profileB });
        _userRepositoryMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { userA, userB });

        // Act
        var result = await _sut.ListEmployeesAsync(companyId, status: null, search: null, page: 1, limit: 20);

        // Assert
        result.Total.Should().Be(1);
        result.Data.Should().ContainSingle(e => e.RecordId == profileA.UserId);
    }

    [Fact]
    public async Task ListEmployeesAsync_GivenStatusFilter_ShouldOnlyReturnMatchingEmployees()
    {
        // Arrange
        var companyId = Guid.NewGuid();
        var (activeProfile, activeUser) = CreateProfileAndUser();
        activeUser.CompanyId = companyId;
        activeUser.IsActive = true;

        var (inactiveProfile, inactiveUser) = CreateProfileAndUser();
        inactiveUser.CompanyId = companyId;
        inactiveUser.IsActive = false;

        _employeeProfileRepositoryMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { activeProfile, inactiveProfile });
        _userRepositoryMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { activeUser, inactiveUser });

        // Act
        var activeResult = await _sut.ListEmployeesAsync(companyId, status: "Active", search: null, page: 1, limit: 20);
        var inactiveResult = await _sut.ListEmployeesAsync(companyId, status: "inactive", search: null, page: 1, limit: 20);

        // Assert
        activeResult.Data.Should().ContainSingle(e => e.RecordId == activeProfile.UserId && e.EmploymentStatus == "Active");
        inactiveResult.Data.Should().ContainSingle(e => e.RecordId == inactiveProfile.UserId && e.EmploymentStatus == "Inactive");
    }

    [Fact]
    public async Task ListEmployeesAsync_ShouldPaginateResults()
    {
        // Arrange
        var companyId = Guid.NewGuid();
        var profiles = new List<EmployeeProfile>();
        var users = new List<User>();
        for (var i = 0; i < 5; i++)
        {
            var (profile, user) = CreateProfileAndUser();
            user.CompanyId = companyId;
            profiles.Add(profile);
            users.Add(user);
        }

        _employeeProfileRepositoryMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(profiles);
        _userRepositoryMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(users);

        // Act
        var result = await _sut.ListEmployeesAsync(companyId, status: null, search: null, page: 2, limit: 2);

        // Assert
        result.Total.Should().Be(5);
        result.Page.Should().Be(2);
        result.Data.Should().HaveCount(2);
    }

    [Fact]
    public async Task ListEmployeesAsync_ShouldIncludeDepartmentName()
    {
        // Arrange
        var companyId = Guid.NewGuid();
        var (profile, user) = CreateProfileAndUser();
        user.CompanyId = companyId;
        var department = new Department { Id = profile.DepartmentId, CompanyId = companyId, Name = "Engineering", IsDeleted = false };

        _employeeProfileRepositoryMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { profile });
        _userRepositoryMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { user });
        _departmentRepositoryMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { department });

        // Act
        var result = await _sut.ListEmployeesAsync(companyId, status: null, search: null, page: 1, limit: 20);

        // Assert
        result.Data.Should().ContainSingle(e => e.Department == "Engineering");
    }

    // ------------------------------------------------------------
    // UpdateEmployeeAsync
    // ------------------------------------------------------------

    [Fact]
    public async Task UpdateEmployeeAsync_GivenUnknownRecordId_ShouldReturnNull()
    {
        // Arrange
        _employeeProfileRepositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmployeeProfile?)null);

        // Act
        var result = await _sut.UpdateEmployeeAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new UpdateEmployeeRequest());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateEmployeeAsync_GivenRecordFromAnotherCompany_ShouldReturnNull()
    {
        // Arrange
        var (profile, user) = CreateProfileAndUser();
        var otherCompanyId = Guid.NewGuid();

        _employeeProfileRepositoryMock.Setup(r => r.GetByIdAsync(profile.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        _userRepositoryMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        // Act
        var result = await _sut.UpdateEmployeeAsync(otherCompanyId, Guid.NewGuid(), profile.UserId, new UpdateEmployeeRequest());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateEmployeeAsync_GivenHireDateInFuture_ShouldThrowAndNotPersist()
    {
        // Arrange
        var (profile, user) = CreateProfileAndUser();

        _employeeProfileRepositoryMock.Setup(r => r.GetByIdAsync(profile.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        _userRepositoryMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var request = new UpdateEmployeeRequest { HireDate = DateTime.UtcNow.AddDays(1) };

        // Act
        var act = () => _sut.UpdateEmployeeAsync(user.CompanyId, Guid.NewGuid(), profile.UserId, request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("hire_date_in_future");
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateEmployeeAsync_GivenPartialFields_ShouldOnlyUpdateProvidedFieldsAndPersist()
    {
        // Arrange
        var (profile, user) = CreateProfileAndUser();
        var originalContractType = profile.ContractType;

        _employeeProfileRepositoryMock.Setup(r => r.GetByIdAsync(profile.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        _userRepositoryMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var request = new UpdateEmployeeRequest
        {
            FullName = "Updated Name",
            Salary = 20000m,
            NationalId = "12345678901234"
        };

        // Act
        var result = await _sut.UpdateEmployeeAsync(user.CompanyId, Guid.NewGuid(), profile.UserId, request);

        // Assert
        result.Should().NotBeNull();
        result!.FullName.Should().Be("Updated Name");
        result.Salary.Should().Be(20000m);
        result.NationalId.Should().Be("12345678901234");
        result.ContractType.Should().Be(originalContractType);

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _userRepositoryMock.Verify(r => r.Update(user), Times.Once);
        _employeeProfileRepositoryMock.Verify(r => r.Update(profile), Times.Once);
    }

    [Fact]
    public async Task UpdateEmployeeAsync_GivenValidDepartmentId_ShouldReassignAndReturnDepartmentName()
    {
        // Arrange
        var (profile, user) = CreateProfileAndUser();
        var newDepartmentId = Guid.NewGuid();
        var newDepartment = new Department { Id = newDepartmentId, CompanyId = user.CompanyId, Name = "Finance", IsDeleted = false };

        _employeeProfileRepositoryMock.Setup(r => r.GetByIdAsync(profile.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        _userRepositoryMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _departmentRepositoryMock.Setup(r => r.GetByIdAsync(newDepartmentId, It.IsAny<CancellationToken>())).ReturnsAsync(newDepartment);

        var request = new UpdateEmployeeRequest { DepartmentId = newDepartmentId };

        // Act
        var result = await _sut.UpdateEmployeeAsync(user.CompanyId, Guid.NewGuid(), profile.UserId, request);

        // Assert
        result.Should().NotBeNull();
        result!.DepartmentId.Should().Be(newDepartmentId);
        result.Department.Should().Be("Finance");
        profile.DepartmentId.Should().Be(newDepartmentId);
    }

    [Fact]
    public async Task UpdateEmployeeAsync_GivenUnknownDepartment_ShouldThrowAndNotPersist()
    {
        // Arrange
        var (profile, user) = CreateProfileAndUser();
        var unknownDepartmentId = Guid.NewGuid();

        _employeeProfileRepositoryMock.Setup(r => r.GetByIdAsync(profile.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        _userRepositoryMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _departmentRepositoryMock.Setup(r => r.GetByIdAsync(unknownDepartmentId, It.IsAny<CancellationToken>())).ReturnsAsync((Department?)null);

        var request = new UpdateEmployeeRequest { DepartmentId = unknownDepartmentId };

        // Act
        var act = () => _sut.UpdateEmployeeAsync(user.CompanyId, Guid.NewGuid(), profile.UserId, request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("department_not_found");
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateEmployeeAsync_GivenDepartmentFromAnotherCompany_ShouldThrowAndNotPersist()
    {
        // Arrange
        var (profile, user) = CreateProfileAndUser();
        var otherCompanyDepartmentId = Guid.NewGuid();
        var otherCompanyDepartment = new Department { Id = otherCompanyDepartmentId, CompanyId = Guid.NewGuid(), Name = "Other Co Dept", IsDeleted = false };

        _employeeProfileRepositoryMock.Setup(r => r.GetByIdAsync(profile.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        _userRepositoryMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _departmentRepositoryMock.Setup(r => r.GetByIdAsync(otherCompanyDepartmentId, It.IsAny<CancellationToken>())).ReturnsAsync(otherCompanyDepartment);

        var request = new UpdateEmployeeRequest { DepartmentId = otherCompanyDepartmentId };

        // Act
        var act = () => _sut.UpdateEmployeeAsync(user.CompanyId, Guid.NewGuid(), profile.UserId, request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("department_not_found");
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ------------------------------------------------------------
    // DeactivateEmployeeAsync
    // ------------------------------------------------------------

    [Fact]
    public async Task DeactivateEmployeeAsync_GivenUnknownRecordId_ShouldReturnFalse()
    {
        // Arrange
        _employeeProfileRepositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmployeeProfile?)null);

        // Act
        var result = await _sut.DeactivateEmployeeAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Assert
        result.Should().BeFalse();
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeactivateEmployeeAsync_GivenRecordFromAnotherCompany_ShouldReturnFalse()
    {
        // Arrange
        var (profile, user) = CreateProfileAndUser();

        _employeeProfileRepositoryMock.Setup(r => r.GetByIdAsync(profile.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        _userRepositoryMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        // Act
        var result = await _sut.DeactivateEmployeeAsync(Guid.NewGuid(), Guid.NewGuid(), profile.UserId);

        // Assert
        result.Should().BeFalse();
        user.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task DeactivateEmployeeAsync_GivenActiveEmployee_ShouldDeactivateAndRevokeTokens()
    {
        // Arrange
        var (profile, user) = CreateProfileAndUser();
        var activeToken = new RefreshToken { Id = Guid.NewGuid(), UserId = user.Id, IsRevoked = false };

        _employeeProfileRepositoryMock.Setup(r => r.GetByIdAsync(profile.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        _userRepositoryMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _refreshTokenRepositoryMock
            .Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<RefreshToken, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RefreshToken> { activeToken });

        // Act
        var result = await _sut.DeactivateEmployeeAsync(user.CompanyId, Guid.NewGuid(), profile.UserId);

        // Assert
        result.Should().BeTrue();
        user.IsActive.Should().BeFalse();
        activeToken.IsRevoked.Should().BeTrue();

        _userRepositoryMock.Verify(r => r.Update(user), Times.Once);
        _refreshTokenRepositoryMock.Verify(r => r.Update(activeToken), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeactivateEmployeeAsync_GivenAlreadyInactiveEmployee_ShouldBeIdempotentAndNotPersist()
    {
        // Arrange
        var (profile, user) = CreateProfileAndUser();
        user.IsActive = false;

        _employeeProfileRepositoryMock.Setup(r => r.GetByIdAsync(profile.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        _userRepositoryMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        // Act
        var result = await _sut.DeactivateEmployeeAsync(user.CompanyId, Guid.NewGuid(), profile.UserId);

        // Assert
        result.Should().BeTrue();
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _userRepositoryMock.Verify(r => r.Update(It.IsAny<User>()), Times.Never);
    }

    private static (EmployeeProfile Profile, User User) CreateProfileAndUser()
    {
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var user = new User
        {
            Id = userId,
            CompanyId = companyId,
            Email = "existing@test.com",
            FullName = "Existing Name",
            IsActive = true
        };

        var profile = new EmployeeProfile
        {
            UserId = userId,
            DepartmentId = Guid.Empty,
            JobTitle = "Analyst",
            Salary = 10000m,
            HireDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-1)),
            ContractType = "Full-Time",
            NationalId = null
        };

        return (profile, user);
    }

    private void SetupValidDepartment(Guid companyId, Guid departmentId)
    {
        _departmentRepositoryMock
            .Setup(r => r.GetByIdAsync(departmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Department { Id = departmentId, CompanyId = companyId, IsDeleted = false });
    }

    private static CreateEmployeeRequest CreateValidRequest()
    {
        return new CreateEmployeeRequest
        {
            FullName = "Nour Hassan",
            Email = $"nour_{Guid.NewGuid():N}@test.com",
            JobTitle = "Software Engineer",
            DepartmentId = Guid.NewGuid(),
            HireDate = DateTime.UtcNow.AddDays(-1),
            Salary = 15000m,
            ContractType = "Full-Time"
        };
    }
}
