using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wakeel.Domain.Entities;

namespace Wakeel.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity Framework configuration for the CompanyHandbook entity.
/// Defines table mappings, constraints, indexes, and relationships.
/// </summary>
public class CompanyHandbookConfiguration : IEntityTypeConfiguration<CompanyHandbook>
{
    /// <summary>
    /// Configures the CompanyHandbook entity mapping in the database.
    /// </summary>
    /// <param name="builder">The entity type builder for CompanyHandbook.</param>
    public void Configure(EntityTypeBuilder<CompanyHandbook> builder)
    {
        builder.ToTable("COMPANY_HANDBOOK");

        builder.HasKey(h => h.Id);

        builder.Property(h => h.Title)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(h => h.FileUrl)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(h => h.UploadedAt)
            .IsRequired();

        builder.HasIndex(h => h.CompanyId);

        builder.HasOne(h => h.Company)
            .WithMany()
            .HasForeignKey(h => h.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(h => h.UploadedByUser)
            .WithMany()
            .HasForeignKey(h => h.UploadedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
