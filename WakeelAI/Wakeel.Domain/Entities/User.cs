using System;
using System.Collections.Generic;
using Wakeel.Domain.Enums;

namespace Wakeel.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public bool IsActive { get; set; }
    public bool IsEmailConfirmed { get; set; }
    public string ActivationToken { get; set; } = string.Empty;
    public DateTime ActivationTokenExpiry { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }

    public Company Company { get; set; } = null!;
    public EmployeeProfile? EmployeeProfile { get; set; }
    public User? CreatedByUser { get; set; }
    public ICollection<User> CreatedUsers { get; set; } = new List<User>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
