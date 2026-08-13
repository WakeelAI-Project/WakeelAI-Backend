using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wakeel.Application.DTOs.AiIntegrations;
using Wakeel.Domain.Entities;
using Wakeel.Infrastructure.Persistence;

namespace Wakeel.API.Controllers;

/// <summary>
/// Internal Machine-to-Machine (M2M) controller for document generation workflows
/// (fetching templates and saving generated documents) initiated by the Node.js AI service.
/// Secured exclusively via InternalApiKeyMiddleware (PSK).
/// </summary>
[ApiController]
[Route("api")]
[AllowAnonymous]
public class InternalAiDocumentController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;

    public InternalAiDocumentController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    private Guid GetXCompanyId() => Guid.Parse(Request.Headers["X-Company-Id"]!);

    [HttpGet("ai/templates/active")]
    public async Task<IActionResult> GetActiveTemplate([FromQuery] string documentType, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(documentType))
            return BadRequest(new { error = new { code = "validation_error", message = "documentType query parameter is required." } });

        var companyId = GetXCompanyId();

        var template = await _dbContext.DocumentTemplates
            .FirstOrDefaultAsync(t => t.IsActive && t.DocumentType == documentType && t.CompanyId == companyId, cancellationToken);

        if (template == null)
            return NotFound(new { error = new { code = "template_not_found", message = "Active template not found." } });

        var response = new ActiveTemplateResponse
        {
            TemplateId = template.Id.ToString(),
            DocumentType = template.DocumentType,
            Name = template.Name,
            ContentTemplate = template.ContentTemplate
        };

        return Ok(response);
    }

    [HttpPost("documents/save")]
    public async Task<IActionResult> SaveGeneratedDocument([FromBody] SaveDocumentRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var companyId = GetXCompanyId();

        // Validate metadata companyId matches the trusted header
        if (!Guid.TryParse(request.Metadata.CompanyId, out var metadataCompanyId) || metadataCompanyId != companyId)
        {
            return NotFound(new { error = new { code = "company_not_found", message = "Cross-tenant request denied." } });
        }

        if (!Guid.TryParse(request.Metadata.EmployeeId, out var employeeId))
        {
            return BadRequest(new { error = new { code = "validation_error", message = "Invalid employeeId format." } });
        }

        // Validate employee exists in the company
        var employeeExists = await _dbContext.EmployeeProfiles
            .AnyAsync(e => e.UserId == employeeId && e.Department.CompanyId == companyId, cancellationToken);

        if (!employeeExists)
        {
            return NotFound(new { error = new { code = "employee_not_found", message = "Employee not found." } });
        }

        var document = new GeneratedDocument
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            EmployeeId = employeeId,
            DocumentType = request.DocumentType,
            Title = request.Title,
            Content = request.Content,
            Status = "Draft",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.GeneratedDocuments.Add(document);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = new SaveDocumentResponse
        {
            Success = true,
            DocumentId = document.Id.ToString(),
            Status = document.Status
        };

        return Ok(response);
    }
}
