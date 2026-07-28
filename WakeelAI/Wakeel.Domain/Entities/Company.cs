using System;
using System.Collections.Generic;

namespace Wakeel.Domain.Entities;

public class Company
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Industry { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public DateTime RegisteredAt { get; set; }
    public bool IsActive { get; set; }

    public ICollection<User> Users { get; set; } = new List<User>();
}
