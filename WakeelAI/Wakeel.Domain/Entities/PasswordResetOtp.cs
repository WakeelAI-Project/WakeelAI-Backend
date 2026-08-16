using System;

namespace Wakeel.Domain.Entities;

public class PasswordResetOtp
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string OtpHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public int FailedAttempts { get; set; }
    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;
}
