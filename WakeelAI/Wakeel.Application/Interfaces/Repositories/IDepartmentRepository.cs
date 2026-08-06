using Wakeel.Domain.Entities;

namespace Wakeel.Application.Interfaces.Repositories;

/// <summary>
/// Repository interface for Department entity.
/// Extends the generic repository with Department-specific data access.
/// </summary>
public interface IDepartmentRepository : IGenericRepository<Department>
{
}
