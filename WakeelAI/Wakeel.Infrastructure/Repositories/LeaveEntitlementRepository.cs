using Wakeel.Application.Interfaces.Repositories;
using Wakeel.Domain.Entities;
using Wakeel.Infrastructure.Persistence;

namespace Wakeel.Infrastructure.Repositories;

public class LeaveEntitlementRepository : GenericRepository<LeaveEntitlement>, ILeaveEntitlementRepository
{
    public LeaveEntitlementRepository(ApplicationDbContext dbContext) : base(dbContext)
    {
    }
}
