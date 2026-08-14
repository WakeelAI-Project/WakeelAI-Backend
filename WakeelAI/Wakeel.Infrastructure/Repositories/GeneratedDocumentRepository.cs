using Wakeel.Application.Interfaces.Repositories;
using Wakeel.Domain.Entities;
using Wakeel.Infrastructure.Persistence;

namespace Wakeel.Infrastructure.Repositories;

public class GeneratedDocumentRepository : GenericRepository<GeneratedDocument>, IGeneratedDocumentRepository
{
    public GeneratedDocumentRepository(ApplicationDbContext dbContext) : base(dbContext)
    {
    }
}
