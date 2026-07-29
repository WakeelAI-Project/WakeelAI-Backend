using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Wakeel.Application.Interfaces.Repositories;
using Wakeel.Domain.Entities;
using Wakeel.Infrastructure.Persistence;

namespace Wakeel.Infrastructure.Repositories;

public class EmployeeProfileRepository : GenericRepository<EmployeeProfile>, IEmployeeProfileRepository
{
    public EmployeeProfileRepository(ApplicationDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<EmployeeProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await DbSet.FirstOrDefaultAsync(ep => ep.UserId == userId, cancellationToken).ConfigureAwait(false);
    }
}
