using System;

namespace Wakeel.Domain.Entities;

public class AuditLog
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public Company Company { get; set; } = null!;
    public User? User { get; set; }
}
