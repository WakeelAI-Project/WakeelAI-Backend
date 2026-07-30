using System;
using System.Collections.Generic;
<<<<<<< HEAD
=======
using Wakeel.Domain.Enums;
>>>>>>> a1e16be97fe87f91487bdd174f6d7b6ddcca41f4

namespace Wakeel.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
<<<<<<< HEAD
    public string Role { get; set; } = string.Empty;
=======
    public UserRole Role { get; set; }
>>>>>>> a1e16be97fe87f91487bdd174f6d7b6ddcca41f4
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
<<<<<<< HEAD
=======
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
>>>>>>> a1e16be97fe87f91487bdd174f6d7b6ddcca41f4
}
