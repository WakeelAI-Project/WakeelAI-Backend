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

        // One-to-Many: Company employs Users
        builder.HasMany(c => c.Users)
            .WithOne(u => u.Company)
            .HasForeignKey(u => u.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
