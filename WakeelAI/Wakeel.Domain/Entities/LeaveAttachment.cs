using System;

namespace Wakeel.Domain.Entities;

/// <summary>
/// Represents a file attachment (e.g., medical report) uploaded prior to 
/// a leave request creation, typically used for Sick leave.
/// </summary>
public class LeaveAttachment
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid EmployeeId { get; set; }
    
    public string Url { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; }
    
    // Navigation properties
    public Company Company { get; set; } = null!;
    public EmployeeProfile Employee { get; set; } = null!;
}
