using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Wakeel.Application.DTOs.Templates;
using Wakeel.Application.Interfaces;
using Wakeel.Domain.Entities;
using Wakeel.Infrastructure.Persistence;

namespace Wakeel.Application.Services;

public class TemplateService : ITemplateService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentTenantService _currentTenantService;

    public TemplateService(ApplicationDbContext dbContext, ICurrentTenantService currentTenantService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentTenantService = currentTenantService ?? throw new ArgumentNullException(nameof(currentTenantService));
    }

    public async Task<(IEnumerable<TemplateDto> Data, int Total)> GetTemplatesAsync(int page, int limit, string? documentType)
    {
        var query = _dbContext.DocumentTemplates.AsQueryable();

        if (!string.IsNullOrWhiteSpace(documentType))
        {
            query = query.Where(t => t.DocumentType == documentType);
        }

        var total = await query.CountAsync();

        var templates = await query
            .OrderBy(t => t.Name)
            .Skip((page - 1) * limit)
            .Take(limit)
            .Select(t => new TemplateDto
            {
                Id = t.Id,
                DocumentType = t.DocumentType,
                Name = t.Name,
                ContentTemplate = t.ContentTemplate,
                IsActive = t.IsActive
            })
            .ToListAsync();

        return (templates, total);
    }

    public async Task<TemplateDto> GetTemplateByIdAsync(Guid id)
    {
        var template = await _dbContext.DocumentTemplates.FindAsync(id);
        if (template == null)
            throw new InvalidOperationException("template_not_found");

        return new TemplateDto
        {
            Id = template.Id,
            DocumentType = template.DocumentType,
            Name = template.Name,
            ContentTemplate = template.ContentTemplate,
            IsActive = template.IsActive
        };
    }

    public async Task<TemplateDto> CreateTemplateAsync(CreateTemplateRequest request)
    {
        if (request.IsActive)
        {
            await DeactivateOtherTemplatesAsync(request.DocumentType, null);
        }

        var template = new DocumentTemplate
        {
            CompanyId = _currentTenantService.CompanyId,
            DocumentType = request.DocumentType,
            Name = request.Name,
            ContentTemplate = request.ContentTemplate,
            IsActive = request.IsActive
        };

        _dbContext.DocumentTemplates.Add(template);
        await _dbContext.SaveChangesAsync();

        return new TemplateDto
        {
            Id = template.Id,
            DocumentType = template.DocumentType,
            Name = template.Name,
            ContentTemplate = template.ContentTemplate,
            IsActive = template.IsActive
        };
    }

    public async Task<TemplateDto> UpdateTemplateAsync(Guid id, UpdateTemplateRequest request)
    {
        var template = await _dbContext.DocumentTemplates.FindAsync(id);
        if (template == null)
            throw new InvalidOperationException("template_not_found");

        if (request.Name != null)
            template.Name = request.Name;

        if (request.ContentTemplate != null)
            template.ContentTemplate = request.ContentTemplate;

        if (request.IsActive.HasValue)
        {
            if (request.IsActive.Value && !template.IsActive)
            {
                await DeactivateOtherTemplatesAsync(template.DocumentType, template.Id);
            }
            template.IsActive = request.IsActive.Value;
        }

        await _dbContext.SaveChangesAsync();

        return new TemplateDto
        {
            Id = template.Id,
            DocumentType = template.DocumentType,
            Name = template.Name,
            ContentTemplate = template.ContentTemplate,
            IsActive = template.IsActive
        };
    }

    public async Task DeleteTemplateAsync(Guid id)
    {
        var template = await _dbContext.DocumentTemplates.FindAsync(id);
        if (template == null)
            throw new InvalidOperationException("template_not_found");

        _dbContext.DocumentTemplates.Remove(template);
        await _dbContext.SaveChangesAsync();
    }

    private async Task DeactivateOtherTemplatesAsync(string documentType, Guid? excludeTemplateId)
    {
        var activeTemplatesQuery = _dbContext.DocumentTemplates
            .Where(t => t.DocumentType == documentType && t.IsActive);

        if (excludeTemplateId.HasValue)
        {
            activeTemplatesQuery = activeTemplatesQuery.Where(t => t.Id != excludeTemplateId.Value);
        }

        var activeTemplates = await activeTemplatesQuery.ToListAsync();
        foreach (var t in activeTemplates)
        {
            t.IsActive = false;
        }
    }
}
