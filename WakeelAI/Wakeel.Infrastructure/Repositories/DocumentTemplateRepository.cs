using Wakeel.Application.Interfaces.Repositories;
using Wakeel.Domain.Entities;
using Wakeel.Infrastructure.Persistence;

namespace Wakeel.Infrastructure.Repositories;

public class DocumentTemplateRepository : GenericRepository<DocumentTemplate>, IDocumentTemplateRepository
{
    public DocumentTemplateRepository(ApplicationDbContext dbContext) : base(dbContext)
    {
    }
}
