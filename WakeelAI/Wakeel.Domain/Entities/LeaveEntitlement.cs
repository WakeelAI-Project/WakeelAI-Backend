using System;

namespace Wakeel.Domain.Entities;

public class LeaveEntitlement
{
    public Guid Id { get; set; }
    public string LeaveType { get; set; } = string.Empty;
    public int? DefaultDays { get; set; }
}
