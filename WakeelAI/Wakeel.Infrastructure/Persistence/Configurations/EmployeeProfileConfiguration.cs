using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wakeel.Domain.Entities;

namespace Wakeel.Infrastructure.Persistence.Configurations;

public class EmployeeProfileConfiguration : IEntityTypeConfiguration<EmployeeProfile>
{
    public void Configure(EntityTypeBuilder<EmployeeProfile> builder)
    {
        builder.ToTable("EMPLOYEE_PROFILE");

        builder.HasKey(ep => ep.UserId);

        builder.Property(ep => ep.DepartmentId).IsRequired();
        builder.Property(ep => ep.JobTitle).IsRequired();
        builder.Property(ep => ep.NationalId).IsRequired();
        builder.Property(ep => ep.ContractType).IsRequired();
        builder.Property(ep => ep.HireDate).IsRequired();

        // Configure standard precision and scale for Salary
        builder.Property(ep => ep.Salary)
            .IsRequired()
            .HasColumnType("decimal(18,2)");
    }
}
