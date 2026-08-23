using System;
using System.Threading;
using System.Threading.Tasks;
using Wakeel.Application.Interfaces.Repositories;
using Wakeel.Application.Interfaces.Services;
using Wakeel.Domain.Entities;

namespace Wakeel.Application.Services;

/// <inheritdoc cref="ILeaveBalanceProvisioningService" />
public class LeaveBalanceProvisioningService : ILeaveBalanceProvisioningService
{
    private static readonly string[] ProvisionedLeaveTypes = { "Annual", "Sick", "Unpaid" };

    private readonly IUnitOfWork _unitOfWork;

    public LeaveBalanceProvisioningService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    /// <inheritdoc />
    public async Task<LeaveBalance> GetOrCreateAsync(Guid employeeId, string leaveType, int year, CancellationToken cancellationToken = default)
    {
        var existing = await _unitOfWork.LeaveBalances.FirstOrDefaultAsync(
            lb => lb.EmployeeId == employeeId && lb.LeaveType == leaveType && lb.Year == year,
            cancellationToken);

        if (existing != null)
            return existing;

        var entitlement = await _unitOfWork.LeaveEntitlements.FirstOrDefaultAsync(
            e => e.LeaveType == leaveType, cancellationToken);

        if (entitlement == null)
            throw new InvalidOperationException($"No LEAVE_ENTITLEMENT row is configured for leave type '{leaveType}'.");

        var profile = await _unitOfWork.EmployeeProfiles.GetByUserIdAsync(employeeId, cancellationToken)
            ?? throw new InvalidOperationException("employee_not_found");

        var balance = new LeaveBalance
        {
            Id = Guid.NewGuid(),
            EmployeeId = employeeId,
            LeaveType = leaveType,
            TotalDays = ComputeTotalDays(entitlement, profile.HireDate, year),
            UsedDays = 0,
            Year = year
        };

        await _unitOfWork.LeaveBalances.AddAsync(balance, cancellationToken);
        return balance;
    }

    /// <inheritdoc />
    public async Task EnsureYearAsync(Guid employeeId, int year, CancellationToken cancellationToken = default)
    {
        foreach (var leaveType in ProvisionedLeaveTypes)
            await GetOrCreateAsync(employeeId, leaveType, year, cancellationToken);
    }

    /// <summary>
    /// Computes the entitlement for one calendar year from LEAVE_ENTITLEMENT and the
    /// employee's hire date, per Egyptian Labour Law No. 14/2025's service-length tiers.
    ///
    /// Service length is evaluated as of 31 December of <paramref name="year"/> (not
    /// "today") because a balance is provisioned lazily, on first use, which for a
    /// future year can happen the moment the employee is hired - e.g. an employee hired
    /// today must be able to request Annual leave starting next 1 January. Year-end is
    /// the latest point at which the employee could still be taking that year's leave,
    /// so it is the conservative anchor for "have they reached this tier during year Y".
    ///
    /// SCOPE NOTE: the law also grants 30 days at age 50+ and 45 days for a disability,
    /// regardless of service length. EmployeeProfile deliberately carries neither a date
    /// of birth nor a disability flag (out of scope for this milestone), so those two
    /// tiers are not computed here - HR applies them via the manual balance override
    /// endpoint (PUT /api/employees/{recordId}/leave-balances/{leaveType}) instead.
    /// </summary>
    private static int? ComputeTotalDays(LeaveEntitlement entitlement, DateOnly hireDate, int year)
    {
        if (!string.Equals(entitlement.LeaveType, "Annual", StringComparison.Ordinal))
            return entitlement.DefaultDays; // null for Sick/Unpaid - uncapped.

        var yearEnd = new DateOnly(year, 12, 31);
        if (yearEnd < hireDate)
            return 0; // Not yet hired during this year at all.

        var minimumServiceMonths = entitlement.MinimumServiceMonths ?? 0;
        var serviceMonths = ServiceMonthsAsOf(hireDate, yearEnd);

        if (serviceMonths < minimumServiceMonths)
            return 0;

        if (serviceMonths < 12)
        {
            var baseDays = entitlement.BaseDays ?? 0;
            var yearStart = new DateOnly(year, 1, 1);
            var periodStart = hireDate > yearStart ? hireDate : yearStart;
            var daysInYear = yearEnd.DayNumber - yearStart.DayNumber + 1;
            var employedDays = yearEnd.DayNumber - periodStart.DayNumber + 1;
            var fraction = (double)employedDays / daysInYear;
            return (int)Math.Round(baseDays * fraction, MidpointRounding.AwayFromZero);
        }

        var seniorityYears = entitlement.SeniorityYears ?? int.MaxValue;
        var serviceYears = serviceMonths / 12;
        return serviceYears >= seniorityYears ? entitlement.SeniorDays ?? 0 : entitlement.StandardDays ?? 0;
    }

    /// <summary>Full completed months of service between <paramref name="hireDate"/> and <paramref name="asOf"/>.</summary>
    private static int ServiceMonthsAsOf(DateOnly hireDate, DateOnly asOf)
    {
        var months = (asOf.Year - hireDate.Year) * 12 + (asOf.Month - hireDate.Month);
        if (asOf.Day < hireDate.Day)
            months--;
        return Math.Max(months, 0);
    }
}
