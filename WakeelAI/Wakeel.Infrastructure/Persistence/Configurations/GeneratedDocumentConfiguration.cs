using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wakeel.Domain.Entities;

namespace Wakeel.Infrastructure.Persistence.Configurations;

public class GeneratedDocumentConfiguration : IEntityTypeConfiguration<GeneratedDocument>
{
    public void Configure(EntityTypeBuilder<GeneratedDocument> builder)
    {
        builder.ToTable("GeneratedDocuments");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.DocumentType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(d => d.Title)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(d => d.Content)
            .IsRequired();

        builder.Property(d => d.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(d => d.CompanyId);
        builder.HasIndex(d => d.EmployeeId);

        builder.HasOne(d => d.Company)
            .WithMany()
            .HasForeignKey(d => d.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.Employee)
            .WithMany()
            .HasForeignKey(d => d.EmployeeId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(d => d.Template)
            .WithMany()
            .HasForeignKey(d => d.TemplateId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.GeneratedByUser)
            .WithMany()
            .HasForeignKey(d => d.GeneratedByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
