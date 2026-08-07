using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wakeel.Domain.Entities;

namespace Wakeel.Infrastructure.Persistence.Configurations;

public class LeaveRequestConfiguration : IEntityTypeConfiguration<LeaveRequest>
{
    public void Configure(EntityTypeBuilder<LeaveRequest> builder)
    {
        builder.ToTable("LeaveRequests");

        builder.HasKey(lr => lr.Id);

        builder.Property(lr => lr.LeaveType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(lr => lr.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(lr => lr.Reason)
            .HasMaxLength(500);

        builder.Property(lr => lr.AttachmentUrl)
            .HasMaxLength(2000);

        builder.Property(lr => lr.HrNote)
            .HasMaxLength(2000);

        builder.HasOne(lr => lr.Employee)
            .WithMany()
            .HasForeignKey(lr => lr.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(lr => lr.Company)
            .WithMany()
            .HasForeignKey(lr => lr.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(lr => lr.ReviewedByUser)
            .WithMany()
            .HasForeignKey(lr => lr.ReviewedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
