using System;

namespace Wakeel.Domain.Entities;

/// <summary>
/// Represents a leave request submitted by an employee.
/// </summary>
public class LeaveRequest
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public Guid CompanyId { get; set; }
    
    public string LeaveType { get; set; } = string.Empty;
    
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    
    public int DaysRequested { get; set; }
    
    public string? Reason { get; set; }
    
    public string Status { get; set; } = string.Empty;
    
    public string? AttachmentUrl { get; set; }
    
    public Guid? ReviewedByUserId { get; set; }
    
    public string? HrNote { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    
    // Navigation properties
    public EmployeeProfile Employee { get; set; } = null!;
    public Company Company { get; set; } = null!;
    public User? ReviewedByUser { get; set; }
}
