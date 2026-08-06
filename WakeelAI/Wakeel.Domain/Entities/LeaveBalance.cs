using System;

namespace Wakeel.Domain.Entities;

public class LeaveBalance
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public string LeaveType { get; set; } = string.Empty;
    public int? TotalDays { get; set; }
    public int UsedDays { get; set; }
    public int Year { get; set; }

    public EmployeeProfile Employee { get; set; } = null!;
}
