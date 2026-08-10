using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wakeel.Domain.Entities;

namespace Wakeel.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("USERS");

        builder.HasKey(u => u.Id);

        builder.HasIndex(u => u.Email).IsUnique();

        builder.Property(u => u.Email).IsRequired();
        builder.Property(u => u.PasswordHash).IsRequired();
        builder.Property(u => u.FullName).IsRequired();
        builder.Property(u => u.Phone).IsRequired();
        builder.Property(u => u.Role).IsRequired().HasConversion<int>();
        builder.Property(u => u.IsActive).IsRequired();
        builder.Property(u => u.IsEmailConfirmed).IsRequired();
        builder.Property(u => u.MustChangePassword).IsRequired();
        builder.Property(u => u.ActivationToken).IsRequired();
        builder.Property(u => u.ActivationTokenExpiry).IsRequired();
        builder.Property(u => u.CreatedAt).IsRequired();

        // One-to-One: User <-> EmployeeProfile
        // User is the principal, EmployeeProfile is the dependent.
        builder.HasOne(u => u.EmployeeProfile)
            .WithOne(ep => ep.User)
            .HasForeignKey<EmployeeProfile>(ep => ep.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Self-Referencing One-to-Many: User (Creator) -> Users (Created)
        builder.HasOne(u => u.CreatedByUser)
            .WithMany(u => u.CreatedUsers)
            .HasForeignKey(u => u.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
