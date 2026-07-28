using System;

namespace Wakeel.Domain.Entities;

public class EmployeeProfile
{
    public Guid UserId { get; set; }
    public Guid DepartmentId { get; set; }
    public string JobTitle { get; set; } = string.Empty;
    public decimal Salary { get; set; }
    public DateOnly HireDate { get; set; }
    public string NationalId { get; set; } = string.Empty;
    public string ContractType { get; set; } = string.Empty;

    public User User { get; set; } = null!;
}
