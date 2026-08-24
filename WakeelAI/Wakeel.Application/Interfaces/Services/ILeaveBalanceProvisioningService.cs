using System;
using System.Threading;
using System.Threading.Tasks;
using Wakeel.Domain.Entities;

namespace Wakeel.Application.Interfaces.Services;

/// <summary>
/// Provisions <see cref="LeaveBalance"/> rows on demand from the authoritative
/// LEAVE_ENTITLEMENT table, so a missing (employee, leaveType, year) balance is treated
/// as an event to provision rather than a user-facing error.
/// </summary>
public interface ILeaveBalanceProvisioningService
{
    /// <summary>
    /// Returns the existing balance for (employeeId, leaveType, year), or computes and
    /// inserts one from LEAVE_ENTITLEMENT and the employee's hire date. Tracks the new
    /// row via the unit of work but does not call SaveChangesAsync - the caller commits.
    /// Never returns null.
    /// </summary>
    Task<LeaveBalance> GetOrCreateAsync(Guid employeeId, string leaveType, int year, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures Annual, Sick, and Unpaid balances all exist for (employeeId, year).
    /// </summary>
    Task EnsureYearAsync(Guid employeeId, int year, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sums DaysRequested across the employee's Pending requests of this leave type
    /// whose start date falls in this year. A Draft reserves nothing (it is not a
    /// commitment) and an Approved request is already folded into UsedDays, so Pending
    /// is the only status still held back from what a balance display or a new
    /// request's validation can treat as available. This is the single source of truth
    /// for that reservation - both a displayed balance and
    /// LeaveRequestService's own validation must call this so the two can never drift
    /// apart the way they did before.
    /// </summary>
    Task<int> GetReservedDaysAsync(Guid employeeId, string leaveType, int year, CancellationToken cancellationToken = default);
}
