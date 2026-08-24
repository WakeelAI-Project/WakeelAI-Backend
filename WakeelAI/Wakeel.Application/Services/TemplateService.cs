using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wakeel.Application.DTOs.Templates;
using Wakeel.Application.Interfaces;
using Wakeel.Domain.Entities;
using Wakeel.Application.Interfaces.Repositories;

namespace Wakeel.Application.Services;

public class TemplateService : ITemplateService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<TemplateService> _logger;

    public TemplateService(IUnitOfWork unitOfWork, ICurrentTenantService currentTenantService, IAuditLogService auditLogService, ILogger<TemplateService> logger)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _currentTenantService = currentTenantService ?? throw new ArgumentNullException(nameof(currentTenantService));
        _auditLogService = auditLogService ?? throw new ArgumentNullException(nameof(auditLogService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Writes an audit entry without letting a failure affect the caller's result.</summary>
    private async Task TryLogAuditAsync(Guid? userId, string action, string details)
    {
        try
        {
            await _auditLogService.LogActionAsync(userId, action, details);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write {Action} audit entry.", action);
        }
    }

    public async Task<(IEnumerable<TemplateDto> Data, int Total)> GetTemplatesAsync(int page, int limit, string? documentType)
    {
        var allTemplates = await _unitOfWork.DocumentTemplates.GetAllAsync();
        var query = allTemplates.AsQueryable();

        if (!string.IsNullOrWhiteSpace(documentType))
        {
            query = query.Where(t => t.DocumentType == documentType);
        }

        var total = query.Count();

        var templates = query
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
            .ToList();

        return (templates, total);
    }

    public async Task<TemplateDto> GetTemplateByIdAsync(Guid id)
    {
        var template = await _unitOfWork.DocumentTemplates.GetByIdAsync(id);
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

    public async Task<TemplateDto> CreateTemplateAsync(Guid actorUserId, CreateTemplateRequest request)
    {
        if (request.IsActive)
        {
            await DeactivateOtherTemplatesAsync(request.DocumentType, null);
        }

        var template = new DocumentTemplate
        {
            Id = Guid.NewGuid(),
            CompanyId = _currentTenantService.CompanyId ?? throw new InvalidOperationException("no_tenant"),
            DocumentType = request.DocumentType,
            Name = request.Name,
            ContentTemplate = request.ContentTemplate,
            IsActive = request.IsActive
        };

        await _unitOfWork.DocumentTemplates.AddAsync(template);
        await _unitOfWork.SaveChangesAsync();

        await TryLogAuditAsync(actorUserId, "TEMPLATE_CREATED", $"Created template \"{template.Name}\" ({template.DocumentType}).");

        return new TemplateDto
        {
            Id = template.Id,
            DocumentType = template.DocumentType,
            Name = template.Name,
            ContentTemplate = template.ContentTemplate,
            IsActive = template.IsActive
        };
    }

    public async Task<TemplateDto> UpdateTemplateAsync(Guid actorUserId, Guid id, UpdateTemplateRequest request)
    {
        var template = await _unitOfWork.DocumentTemplates.GetByIdAsync(id);
        if (template == null)
            throw new InvalidOperationException("template_not_found");

        if (!string.IsNullOrWhiteSpace(request.DocumentType))
            template.DocumentType = request.DocumentType;

        if (request.Name != null)
            template.Name = request.Name;

        if (request.ContentTemplate != null)
            template.ContentTemplate = request.ContentTemplate;

        var isBeingActivated = false;
        if (request.IsActive.HasValue)
        {
            if (request.IsActive.Value && !template.IsActive)
            {
                await DeactivateOtherTemplatesAsync(template.DocumentType, template.Id);
                isBeingActivated = true;
            }
            template.IsActive = request.IsActive.Value;
        }

        _unitOfWork.DocumentTemplates.Update(template);
        await _unitOfWork.SaveChangesAsync();

        await TryLogAuditAsync(actorUserId, isBeingActivated ? "TEMPLATE_ACTIVATED" : "TEMPLATE_UPDATED", $"Updated template \"{template.Name}\" ({template.DocumentType}).");

        return new TemplateDto
        {
            Id = template.Id,
            DocumentType = template.DocumentType,
            Name = template.Name,
            ContentTemplate = template.ContentTemplate,
            IsActive = template.IsActive
        };
    }

    public async Task DeleteTemplateAsync(Guid actorUserId, Guid id)
    {
        var template = await _unitOfWork.DocumentTemplates.GetByIdAsync(id);
        if (template == null)
            throw new InvalidOperationException("template_not_found");

        _unitOfWork.DocumentTemplates.Remove(template);
        await _unitOfWork.SaveChangesAsync();

        await TryLogAuditAsync(actorUserId, "TEMPLATE_DELETED", $"Deleted template \"{template.Name}\" ({template.DocumentType}).");
    }

    private async Task DeactivateOtherTemplatesAsync(string documentType, Guid? excludeTemplateId)
    {
        var allActive = await _unitOfWork.DocumentTemplates.FindAsync(t => t.DocumentType == documentType && t.IsActive);
        
        var toDeactivate = allActive.AsEnumerable();

        if (excludeTemplateId.HasValue)
        {
            toDeactivate = toDeactivate.Where(t => t.Id != excludeTemplateId.Value);
        }

        foreach (var t in toDeactivate)
        {
            t.IsActive = false;
            _unitOfWork.DocumentTemplates.Update(t);
        }
    }
}
