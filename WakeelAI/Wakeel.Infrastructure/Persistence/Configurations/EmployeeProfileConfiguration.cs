using System;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wakeel.Domain.Entities;
using Wakeel.Infrastructure.Security;

namespace Wakeel.Infrastructure.Persistence.Configurations;

/// <summary>
/// FIX-26: not auto-discovered by <c>ApplyConfigurationsFromAssembly</c> - it needs
/// <see cref="IFieldEncryptionService"/> injected, so <see cref="Persistence.ApplicationDbContext"/>
/// excludes this type from the assembly scan and applies it manually with the service resolved
/// through DI, per <c>ApplicationDbContext</c>'s own constructor.
/// </summary>
public class EmployeeProfileConfiguration : IEntityTypeConfiguration<EmployeeProfile>
{
    private readonly IFieldEncryptionService _fieldEncryptionService;

    public EmployeeProfileConfiguration(IFieldEncryptionService fieldEncryptionService)
    {
        _fieldEncryptionService = fieldEncryptionService ?? throw new ArgumentNullException(nameof(fieldEncryptionService));
    }

    public void Configure(EntityTypeBuilder<EmployeeProfile> builder)
    {
        builder.ToTable("EMPLOYEE_PROFILE");

        builder.HasKey(ep => ep.UserId);

        builder.Property(ep => ep.DepartmentId).IsRequired();
        builder.Property(ep => ep.JobTitle).IsRequired();
        builder.Property(ep => ep.ContractType).IsRequired();
        builder.Property(ep => ep.HireDate).IsRequired();
        builder.Property(ep => ep.TimeZoneId).IsRequired(false).HasMaxLength(100);

        // FIX-26: encrypted at rest (AES-256-GCM - see FieldEncryptionService). Ciphertext is
        // not a fixed-length value, so the column stays nvarchar(max).
        builder.Property(ep => ep.NationalId)
            .IsRequired(false)
            .HasConversion(
                plaintext => plaintext == null ? null : _fieldEncryptionService.Encrypt(plaintext),
                stored => stored == null ? null : _fieldEncryptionService.Decrypt(stored))
            .HasColumnType("nvarchar(max)");

        // FIX-26: encrypted at rest. Column type is nvarchar(max), not decimal(18,2) -
        // ciphertext is not a number. "G" + InvariantCulture round-trips exactly regardless
        // of the server's locale (decimal separator, digit grouping, etc.).
        builder.Property(ep => ep.Salary)
            .IsRequired()
            .HasConversion(
                salary => _fieldEncryptionService.Encrypt(salary.ToString("G", CultureInfo.InvariantCulture)),
                stored => decimal.Parse(_fieldEncryptionService.Decrypt(stored), CultureInfo.InvariantCulture))
            .HasColumnType("nvarchar(max)");

        builder.HasOne(ep => ep.Department)
            .WithMany()
            .HasForeignKey(ep => ep.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
