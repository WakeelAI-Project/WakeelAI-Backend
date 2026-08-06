using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wakeel.Domain.Entities;

namespace Wakeel.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity Framework configuration for the Department entity.
/// Defines table mappings, constraints, indexes, and relationships.
/// </summary>
public class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    /// <summary>
    /// Configures the Department entity mapping in the database.
    /// </summary>
    /// <param name="builder">The entity type builder for Department.</param>
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable("DEPARTMENT");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(d => d.Description)
            .HasMaxLength(500);

        builder.Property(d => d.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(d => d.CreatedAt)
            .IsRequired();

        builder.HasIndex(d => d.CompanyId);

        builder.HasOne(d => d.Company)
            .WithMany()
            .HasForeignKey(d => d.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
