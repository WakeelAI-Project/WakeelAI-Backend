using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Wakeel.Application.Interfaces.Repositories;
using Wakeel.Application.Services;
using Wakeel.Domain.Entities;
using Xunit;

namespace Wakeel.Tests.Unit.Services;

/// <summary>
/// FIX-01: covers the Annual service-length tiers (Egyptian Labour Law No. 14/2025) computed
/// by LeaveBalanceProvisioningService. All hire dates below are fixed, historical calendar
/// dates - never relative to "today" - so the exact tier boundaries stay deterministic no
/// matter which day this suite happens to run on.
/// </summary>
public class LeaveBalanceProvisioningServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ILeaveBalanceRepository> _leaveBalanceRepositoryMock = new();
    private readonly Mock<ILeaveEntitlementRepository> _leaveEntitlementRepositoryMock = new();
    private readonly Mock<IEmployeeProfileRepository> _employeeProfileRepositoryMock = new();

    private readonly LeaveBalanceProvisioningService _sut;

    private static readonly LeaveEntitlement AnnualEntitlement = new()
    {
        LeaveType = "Annual",
        BaseDays = 15,
        StandardDays = 21,
        SeniorDays = 30,
        SeniorityYears = 10,
        MinimumServiceMonths = 6
    };

    private static readonly LeaveEntitlement SickEntitlement = new() { LeaveType = "Sick", DefaultDays = null };
    private static readonly LeaveEntitlement UnpaidEntitlement = new() { LeaveType = "Unpaid", DefaultDays = null };

    public LeaveBalanceProvisioningServiceTests()
    {
        _unitOfWorkMock.Setup(u => u.LeaveBalances).Returns(_leaveBalanceRepositoryMock.Object);
        _unitOfWorkMock.Setup(u => u.LeaveEntitlements).Returns(_leaveEntitlementRepositoryMock.Object);
        _unitOfWorkMock.Setup(u => u.EmployeeProfiles).Returns(_employeeProfileRepositoryMock.Object);

        _leaveEntitlementRepositoryMock
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<LeaveEntitlement, bool>>>(), It.IsAny<CancellationToken>()))
            .Returns<Expression<Func<LeaveEntitlement, bool>>, CancellationToken>((predicate, _) =>
            {
                var all = new[] { AnnualEntitlement, SickEntitlement, UnpaidEntitlement };
                return Task.FromResult(all.FirstOrDefault(predicate.Compile()));
            });

        // No existing balance by default - every test exercises fresh provisioning
        // unless it overrides this.
        _leaveBalanceRepositoryMock
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<LeaveBalance, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveBalance?)null);

        _sut = new LeaveBalanceProvisioningService(_unitOfWorkMock.Object);
    }

    private void SetupEmployeeHiredOn(Guid employeeId, DateOnly hireDate)
    {
        _employeeProfileRepositoryMock
            .Setup(r => r.GetByUserIdAsync(employeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmployeeProfile { UserId = employeeId, HireDate = hireDate });
    }

    [Fact]
    public async Task GetOrCreateAsync_GivenExistingBalance_ReturnsItWithoutCreatingANewOne()
    {
        var employeeId = Guid.NewGuid();
        var existing = new LeaveBalance { Id = Guid.NewGuid(), EmployeeId = employeeId, LeaveType = "Annual", Year = 2026, TotalDays = 21, UsedDays = 3 };
        _leaveBalanceRepositoryMock
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<LeaveBalance, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await _sut.GetOrCreateAsync(employeeId, "Annual", 2026, CancellationToken.None);

        result.Should().BeSameAs(existing);
        _leaveBalanceRepositoryMock.Verify(r => r.AddAsync(It.IsAny<LeaveBalance>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetOrCreateAsync_Annual_UnderSixMonthsServiceByYearEnd_ReturnsZero()
    {
        var employeeId = Guid.NewGuid();
        // Hired 15 Nov 2020: by 31 Dec 2020, only 1 completed month of service.
        SetupEmployeeHiredOn(employeeId, new DateOnly(2020, 11, 15));

        var balance = await _sut.GetOrCreateAsync(employeeId, "Annual", 2020, CancellationToken.None);

        balance.TotalDays.Should().Be(0);
        balance.UsedDays.Should().Be(0);
        balance.Year.Should().Be(2020);
    }

    [Fact]
    public async Task GetOrCreateAsync_Annual_SixToTwelveMonthsServiceByYearEnd_ReturnsProRatedBaseDays()
    {
        var employeeId = Guid.NewGuid();
        // Hired 15 Jun 2020: by 31 Dec 2020, exactly 6 completed months of service -
        // right at the minimum-service threshold, so the pro-rated (not zero) branch applies.
        var hireDate = new DateOnly(2020, 6, 15);
        SetupEmployeeHiredOn(employeeId, hireDate);

        var balance = await _sut.GetOrCreateAsync(employeeId, "Annual", 2020, CancellationToken.None);

        // Employed 15 Jun - 31 Dec out of a 366-day leap year, rounded to the nearest whole day.
        var yearStart = new DateOnly(2020, 1, 1);
        var yearEnd = new DateOnly(2020, 12, 31);
        var daysInYear = yearEnd.DayNumber - yearStart.DayNumber + 1;
        var employedDays = yearEnd.DayNumber - hireDate.DayNumber + 1;
        var expected = (int)Math.Round(15 * ((double)employedDays / daysInYear), MidpointRounding.AwayFromZero);
        balance.TotalDays.Should().Be(expected);
        balance.TotalDays.Should().BeInRange(1, 14, "a partial first year must be less than the full 15-day base");
    }

    [Fact]
    public async Task GetOrCreateAsync_Annual_SecondYearOfService_ReturnsStandardTwentyOneDays()
    {
        var employeeId = Guid.NewGuid();
        // Hired 1 Jan 2019: by 31 Dec 2020, 2 full years of service - past year 1, well under 10.
        SetupEmployeeHiredOn(employeeId, new DateOnly(2019, 1, 1));

        var balance = await _sut.GetOrCreateAsync(employeeId, "Annual", 2020, CancellationToken.None);

        balance.TotalDays.Should().Be(21);
    }

    [Fact]
    public async Task GetOrCreateAsync_Annual_TenYearsOfService_ReturnsSeniorThirtyDays()
    {
        var employeeId = Guid.NewGuid();
        // Hired 1 Jan 2010: by 31 Dec 2020, exactly 11 full years of service - at/over the
        // 10-year seniority threshold.
        SetupEmployeeHiredOn(employeeId, new DateOnly(2010, 1, 1));

        var balance = await _sut.GetOrCreateAsync(employeeId, "Annual", 2020, CancellationToken.None);

        balance.TotalDays.Should().Be(30);
    }

    [Fact]
    public async Task GetOrCreateAsync_Sick_IsUncapped()
    {
        var employeeId = Guid.NewGuid();
        SetupEmployeeHiredOn(employeeId, new DateOnly(2010, 1, 1));

        var balance = await _sut.GetOrCreateAsync(employeeId, "Sick", 2020, CancellationToken.None);

        balance.TotalDays.Should().BeNull();
    }

    [Fact]
    public async Task GetOrCreateAsync_Unpaid_IsUncapped()
    {
        var employeeId = Guid.NewGuid();
        SetupEmployeeHiredOn(employeeId, new DateOnly(2010, 1, 1));

        var balance = await _sut.GetOrCreateAsync(employeeId, "Unpaid", 2020, CancellationToken.None);

        balance.TotalDays.Should().BeNull();
    }

    [Fact]
    public async Task GetOrCreateAsync_FreshlyHiredEmployee_CanProvisionNextCalendarYearAtStandardTier()
    {
        var employeeId = Guid.NewGuid();
        // Hired 1 Jan 2020: by 31 Dec 2021 (the *next* year), well over a year of service.
        // This is what makes "hired today, request leave starting next January" succeed.
        SetupEmployeeHiredOn(employeeId, new DateOnly(2020, 1, 1));

        var balance = await _sut.GetOrCreateAsync(employeeId, "Annual", 2021, CancellationToken.None);

        balance.TotalDays.Should().Be(21);
    }

    [Fact]
    public async Task EnsureYearAsync_ProvisionsAllThreeLeaveTypes()
    {
        var employeeId = Guid.NewGuid();
        SetupEmployeeHiredOn(employeeId, new DateOnly(2010, 1, 1));

        var added = new System.Collections.Generic.List<LeaveBalance>();
        _leaveBalanceRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<LeaveBalance>(), It.IsAny<CancellationToken>()))
            .Callback<LeaveBalance, CancellationToken>((lb, _) => added.Add(lb))
            .Returns(Task.CompletedTask);

        await _sut.EnsureYearAsync(employeeId, 2020, CancellationToken.None);

        added.Should().HaveCount(3);
        added.Select(lb => lb.LeaveType).Should().BeEquivalentTo(new[] { "Annual", "Sick", "Unpaid" });
        added.Should().OnlyContain(lb => lb.EmployeeId == employeeId && lb.Year == 2020 && lb.UsedDays == 0);
    }
}
