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

    public User User { get; set; } = null!;
    public Department Department { get; set; } = null!;
    public ICollection<LeaveBalance> LeaveBalances { get; set; } = new List<LeaveBalance>();
}
