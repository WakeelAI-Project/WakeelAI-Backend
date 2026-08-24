using System;
using System.Collections.Generic;

namespace Wakeel.Domain.Entities;

public class EmployeeProfile
{
    public Guid UserId { get; set; }
    public Guid DepartmentId { get; set; }
    public string JobTitle { get; set; } = string.Empty;

    /// <summary>
    /// FIX-26: encrypted at rest (AES-256-GCM, see <c>EmployeeProfileConfiguration</c> /
    /// <c>FieldEncryptionService</c>) via an EF Core value converter - transparent on every
    /// normal read/write. <b>Must never appear in a LINQ <c>Where</c>, <c>OrderBy</c>,
    /// <c>Sum</c>/<c>Average</c>/<c>Min</c>/<c>Max</c>, or any other predicate/aggregate.</b>
    /// EF Core translates those to SQL against the raw ciphertext column, comparing
    /// encrypted bytes instead of the salary value - it will not throw, it will just
    /// silently return the wrong rows/wrong numbers.
    /// </summary>
    public decimal Salary { get; set; }

    public DateOnly HireDate { get; set; }

    /// <summary>
    /// FIX-26: encrypted at rest (AES-256-GCM, see <c>EmployeeProfileConfiguration</c> /
    /// <c>FieldEncryptionService</c>) via an EF Core value converter - transparent on every
    /// normal read/write. <b>Must never appear in a LINQ <c>Where</c>, <c>OrderBy</c>,
    /// <c>Contains</c>/<c>Any</c>, or any other predicate/aggregate.</b> EF Core translates
    /// those to SQL against the raw ciphertext column, comparing encrypted bytes instead of
    /// the national ID - it will not throw, it will just silently return the wrong rows.
    /// </summary>
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
