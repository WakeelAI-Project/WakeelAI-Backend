using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Wakeel.Application.DTOs.Documents;
using Wakeel.Application.Interfaces;
using Wakeel.Domain.Entities;
using Wakeel.Infrastructure.Persistence; // Assume DbContext is accessible or through IUnitOfWork

namespace Wakeel.Application.Services;

public class DocumentService : IDocumentService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly IPdfGeneratorService _pdfGeneratorService;
    private readonly IEmailSender _emailSender;

    public DocumentService(
        ApplicationDbContext dbContext,
        ICurrentTenantService currentTenantService,
        IPdfGeneratorService pdfGeneratorService,
        IEmailSender emailSender)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentTenantService = currentTenantService ?? throw new ArgumentNullException(nameof(currentTenantService));
        _pdfGeneratorService = pdfGeneratorService ?? throw new ArgumentNullException(nameof(pdfGeneratorService));
        _emailSender = emailSender ?? throw new ArgumentNullException(nameof(emailSender));
    }

    public async Task<(IEnumerable<DocumentSummary> Data, int Total)> GetDocumentsAsync(
        int page, int limit, string? type, string? status, Guid? employeeId, string? sort, string? order)
    {
        var query = _dbContext.GeneratedDocuments.AsQueryable();

        if (!string.IsNullOrEmpty(type))
            query = query.Where(d => d.DocumentType == type);
        
        if (!string.IsNullOrEmpty(status))
            query = query.Where(d => d.Status == status);

        if (employeeId.HasValue)
            query = query.Where(d => d.EmployeeId == employeeId.Value);

        // Sorting
        var isAsc = string.Equals(order, "asc", StringComparison.OrdinalIgnoreCase);
        query = sort?.ToLower() switch
        {
            "created_at" => isAsc ? query.OrderBy(d => d.CreatedAt) : query.OrderByDescending(d => d.CreatedAt),
            "updated_at" => isAsc ? query.OrderBy(d => d.UpdatedAt) : query.OrderByDescending(d => d.UpdatedAt),
            "title" => isAsc ? query.OrderBy(d => d.Title) : query.OrderByDescending(d => d.Title),
            _ => query.OrderByDescending(d => d.CreatedAt)
        };

        var total = await query.CountAsync();

        var documents = await query
            .Skip((page - 1) * limit)
            .Take(limit)
            .Select(d => new DocumentSummary
            {
                Id = d.Id,
                DocumentType = d.DocumentType,
                Title = d.Title,
                Status = d.Status,
                EmployeeId = d.EmployeeId,
                CreatedAt = d.CreatedAt,
                UpdatedAt = d.UpdatedAt
            })
            .ToListAsync();

        return (documents, total);
    }

    public async Task<DocumentDetail> GetDocumentByIdAsync(Guid documentId)
    {
        var document = await _dbContext.GeneratedDocuments
            .FirstOrDefaultAsync(d => d.Id == documentId);

        if (document == null)
            throw new InvalidOperationException("document_not_found");

        return new DocumentDetail
        {
            Id = document.Id,
            DocumentType = document.DocumentType,
            Title = document.Title,
            Status = document.Status,
            ContentHtml = document.Status == "Draft" ? document.Content : null,
            PdfUrl = document.Status == "Finalized" ? document.PdfUrl : null,
            EmployeeId = document.EmployeeId,
            TemplateId = document.TemplateId,
            CreatedAt = document.CreatedAt,
            UpdatedAt = document.UpdatedAt,
            FinalizedAt = document.FinalizedAt
        };
    }

    public async Task UpdateDocumentAsync(Guid documentId, UpdateDocumentRequest request)
    {
        var document = await _dbContext.GeneratedDocuments
            .FirstOrDefaultAsync(d => d.Id == documentId);

        if (document == null)
            throw new InvalidOperationException("document_not_found");

        if (document.Status != "Draft")
            throw new InvalidOperationException("not_a_draft");

        if (request.Title != null)
            document.Title = request.Title;

        if (request.ContentHtml != null)
            document.Content = request.ContentHtml;

        document.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();
    }

    public async Task FinalizeDocumentAsync(Guid documentId)
    {
        var document = await _dbContext.GeneratedDocuments
            .FirstOrDefaultAsync(d => d.Id == documentId);

        if (document == null)
            throw new InvalidOperationException("document_not_found");

        if (document.Status != "Draft")
            throw new InvalidOperationException("not_a_draft");

        if (string.IsNullOrWhiteSpace(document.Content))
            throw new InvalidOperationException("document_has_no_content");

        // Generate PDF
        var pdfUrl = await _pdfGeneratorService.GeneratePdfFromHtmlAsync(document.Content, document.Title);

        document.Status = "Finalized";
        document.PdfUrl = pdfUrl;
        document.FinalizedAt = DateTime.UtcNow;
        document.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();
    }

    public async Task SendEmailAsync(Guid documentId, SendEmailRequest request)
    {
        var document = await _dbContext.GeneratedDocuments
            .Include(d => d.Employee)
            .FirstOrDefaultAsync(d => d.Id == documentId);

        if (document == null)
            throw new InvalidOperationException("document_not_found");

        if (document.Status != "Finalized")
            throw new InvalidOperationException("not_finalized");

        var emailTo = request.EmailTo;
        if (string.IsNullOrWhiteSpace(emailTo))
        {
            if (document.Employee == null || string.IsNullOrWhiteSpace(document.Employee.Email))
                throw new InvalidOperationException("employee_no_email");
            emailTo = document.Employee.Email;
        }

        try
        {
            var htmlBody = $"<p>Hello,</p><p>Please find your document '{document.Title}' at the following link:</p><p><a href=\"{document.PdfUrl}\">Download PDF</a></p>";
            await _emailSender.SendEmailAsync(emailTo, $"Document: {document.Title}", htmlBody);
        }
        catch (Exception)
        {
            throw new InvalidOperationException("email_send_failed");
        }

        document.EmailSentTo = emailTo;
        document.EmailSentAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();
    }
}
