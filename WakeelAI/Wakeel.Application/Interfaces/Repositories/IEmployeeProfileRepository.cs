using System;
using System.Threading;
using System.Threading.Tasks;
using Wakeel.Domain.Entities;

namespace Wakeel.Application.Interfaces.Repositories;

public interface IEmployeeProfileRepository : IGenericRepository<EmployeeProfile>
{
    Task<EmployeeProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
}
