using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wakeel.Domain.Entities;

namespace Wakeel.Infrastructure.Persistence.Configurations;

public class LeaveBalanceConfiguration : IEntityTypeConfiguration<LeaveBalance>
{
    public void Configure(EntityTypeBuilder<LeaveBalance> builder)
    {
        builder.ToTable("LEAVE_BALANCE");

        builder.HasKey(lb => lb.Id);

        builder.Property(lb => lb.LeaveType).IsRequired();
        builder.Property(lb => lb.TotalDays).IsRequired(false);
        builder.Property(lb => lb.UsedDays).IsRequired();
        builder.Property(lb => lb.Year).IsRequired();

        builder.HasOne(lb => lb.Employee)
            .WithMany(ep => ep.LeaveBalances)
            .HasForeignKey(lb => lb.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
