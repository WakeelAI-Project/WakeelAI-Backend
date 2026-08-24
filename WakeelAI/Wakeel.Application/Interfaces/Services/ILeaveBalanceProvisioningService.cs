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
}
