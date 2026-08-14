using Wakeel.Application.Interfaces.Repositories;
using Wakeel.Domain.Entities;
using Wakeel.Infrastructure.Persistence;

namespace Wakeel.Infrastructure.Repositories;

public class AuditLogRepository : GenericRepository<AuditLog>, IAuditLogRepository
{
    public AuditLogRepository(ApplicationDbContext dbContext) : base(dbContext)
    {
    }
}
