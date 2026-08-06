using Wakeel.Application.Interfaces.Repositories;
using Wakeel.Domain.Entities;
using Wakeel.Infrastructure.Persistence;

namespace Wakeel.Infrastructure.Repositories;

/// <summary>
/// Implementation of IDepartmentRepository.
/// Inherits generic CRUD operations from GenericRepository.
/// </summary>
public class DepartmentRepository : GenericRepository<Department>, IDepartmentRepository
{
    /// <summary>
    /// Initializes a new instance of the DepartmentRepository class.
    /// </summary>
    /// <param name="dbContext">The application database context.</param>
    public DepartmentRepository(ApplicationDbContext dbContext) : base(dbContext)
    {
    }
}
