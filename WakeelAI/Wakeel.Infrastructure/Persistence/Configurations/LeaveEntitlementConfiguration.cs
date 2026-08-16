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

        // Seed default entitlements with stable GUIDs to avoid non-deterministic model
        builder.HasData(
            new LeaveEntitlement { Id = new Guid("11111111-1111-1111-1111-111111111111"), LeaveType = "Annual", DefaultDays = 15 },
            new LeaveEntitlement { Id = new Guid("22222222-2222-2222-2222-222222222222"), LeaveType = "Sick", DefaultDays = 10 },
            new LeaveEntitlement { Id = new Guid("33333333-3333-3333-3333-333333333333"), LeaveType = "Unpaid", DefaultDays = null }
        );
    }
}
