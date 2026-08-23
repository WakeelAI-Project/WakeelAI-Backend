using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wakeel.Domain.Entities;

namespace Wakeel.Infrastructure.Persistence.Configurations;

public class LeaveEntitlementConfiguration : IEntityTypeConfiguration<LeaveEntitlement>
{
    public void Configure(EntityTypeBuilder<LeaveEntitlement> builder)
    {
        builder.ToTable("LEAVE_ENTITLEMENT");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.LeaveType).IsRequired().HasMaxLength(50);
        builder.Property(e => e.DefaultDays).IsRequired(false);
        builder.Property(e => e.BaseDays).IsRequired(false);
        builder.Property(e => e.StandardDays).IsRequired(false);
        builder.Property(e => e.SeniorDays).IsRequired(false);
        builder.Property(e => e.SeniorityYears).IsRequired(false);
        builder.Property(e => e.MinimumServiceMonths).IsRequired(false);

        // Seed default entitlements with stable GUIDs to avoid non-deterministic model.
        // Annual is tiered by service length (Egyptian Labour Law No. 14/2025) via the
        // Base/Standard/Senior columns - DefaultDays is unused for it. Sick and Unpaid
        // are uncapped (DefaultDays = null): the law defines no fixed "sick days" quota
        // (it is staged paid-through-insurance instead) and no statutory unpaid quota.
        builder.HasData(
            new LeaveEntitlement
            {
                Id = new Guid("11111111-1111-1111-1111-111111111111"),
                LeaveType = "Annual",
                BaseDays = 15,
                StandardDays = 21,
                SeniorDays = 30,
                SeniorityYears = 10,
                MinimumServiceMonths = 6
            },
            new LeaveEntitlement { Id = new Guid("22222222-2222-2222-2222-222222222222"), LeaveType = "Sick", DefaultDays = null },
            new LeaveEntitlement { Id = new Guid("33333333-3333-3333-3333-333333333333"), LeaveType = "Unpaid", DefaultDays = null }
        );
    }
}
