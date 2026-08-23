using System;
using System.Collections.Generic;

namespace Wakeel.Domain.Entities;

public class EmployeeProfile
{
    public Guid UserId { get; set; }
    public Guid DepartmentId { get; set; }
    public string JobTitle { get; set; } = string.Empty;
    public decimal Salary { get; set; }
    public DateOnly HireDate { get; set; }
    public string? NationalId { get; set; }
    public string ContractType { get; set; } = string.Empty;

    /// IANA time zone identifier (e.g. "Africa/Cairo"), reported by the
    /// employee's mobile device. Null until the app has synced it at least
    /// once, in which case date-sensitive calculations (e.g. CurrentLeave's
    /// ElapsedDays) fall back to UTC.
    public string? TimeZoneId { get; set; }

    public User User { get; set; } = null!;
    public Department Department { get; set; } = null!;
    public ICollection<LeaveBalance> LeaveBalances { get; set; } = new List<LeaveBalance>();
}
