using System;

namespace Wakeel.Domain.Entities;

public class LeaveEntitlement
{
    public Guid Id { get; set; }
    public string LeaveType { get; set; } = string.Empty;

    /// <summary>
    /// Uncapped entitlement (e.g. Sick, Unpaid). Ignored for "Annual", which is computed
    /// from the tier columns below instead.
    /// </summary>
    public int? DefaultDays { get; set; }

    /// <summary>Annual days granted for 6 months to under 1 year of service (pro-rated).</summary>
    public int? BaseDays { get; set; }

    /// <summary>Annual days granted from the second year of service up to <see cref="SeniorityYears"/>.</summary>
    public int? StandardDays { get; set; }

    /// <summary>Annual days granted once service reaches <see cref="SeniorityYears"/>.</summary>
    public int? SeniorDays { get; set; }

    /// <summary>Years of service at which <see cref="SeniorDays"/> applies instead of <see cref="StandardDays"/>.</summary>
    public int? SeniorityYears { get; set; }

    /// <summary>Minimum months of service before any annual leave accrues.</summary>
    public int? MinimumServiceMonths { get; set; }
}
