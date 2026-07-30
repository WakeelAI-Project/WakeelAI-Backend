using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wakeel.Domain.Entities;

namespace Wakeel.Infrastructure.Persistence.Configurations;

public class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("COMPANY");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name)
            .IsRequired();

        builder.Property(c => c.Industry)
            .IsRequired();

        builder.Property(c => c.Address)
            .IsRequired();

        builder.Property(c => c.RegisteredAt)
            .IsRequired();

        builder.Property(c => c.IsActive)
            .IsRequired();

        builder.Property(c => c.PhoneNumber)
            .HasMaxLength(50)
            .IsRequired(false);

        builder.Property(c => c.Email)
            .HasMaxLength(256)
            .IsRequired(false);

        builder.Property(c => c.LogoUrl)
            .HasMaxLength(1000)
            .IsRequired(false);

        builder.Property(c => c.WorkingHours)
            .HasMaxLength(500)
            .IsRequired(false);

        // One-to-Many: Company employs Users
        builder.HasMany(c => c.Users)
            .WithOne(u => u.Company)
            .HasForeignKey(u => u.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
