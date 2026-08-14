using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wakeel.Application.DTOs.Documents;
using Wakeel.Application.Interfaces;

namespace Wakeel.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentService _documentService;

    public DocumentsController(IDocumentService documentService)
    {
        _documentService = documentService ?? throw new ArgumentNullException(nameof(documentService));
    }

    [HttpGet]
    [Authorize(Roles = "HR_Manager, Employee")]
    public async Task<IActionResult> GetDocuments(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        [FromQuery] string? type = null,
        [FromQuery] string? status = null,
        [FromQuery] Guid? employee_id = null,
        [FromQuery] string? sort = null,
        [FromQuery] string? order = null)
    {
        var userRole = User.FindFirstValue(ClaimTypes.Role);
        var currentUserIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);

        // Enforce employee own-only rule
        Guid? filterEmployeeId = employee_id;
        if (userRole == "Employee")
        {
            if (Guid.TryParse(currentUserIdStr, out var currentUserId))
            {
                filterEmployeeId = currentUserId;
            }
        }

        var (documents, total) = await _documentService.GetDocumentsAsync(page, limit, type, status, filterEmployeeId, sort, order);

        return Ok(new
        {
            data = documents,
            page = page,
            limit = limit,
            total = total
        });
    }

    [HttpGet("{doc_id}")]
    [Authorize(Roles = "HR_Manager, Employee")]
    public async Task<IActionResult> GetDocument(Guid doc_id)
    {
        var document = await _documentService.GetDocumentByIdAsync(doc_id);

        var userRole = User.FindFirstValue(ClaimTypes.Role);
        var currentUserIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);

        // Enforce employee own-only rule
        if (userRole == "Employee")
        {
            if (!Guid.TryParse(currentUserIdStr, out var currentUserId) || document.EmployeeId != currentUserId)
            {
                return NotFound(new { error = new { code = "document_not_found", message = "Document not found." } });
            }
        }

        return Ok(document);
    }

    [HttpPatch("{doc_id}")]
    [Authorize(Roles = "HR_Manager")]
    public async Task<IActionResult> UpdateDocument(Guid doc_id, [FromBody] UpdateDocumentRequest request)
    {
        await _documentService.UpdateDocumentAsync(doc_id, request);
        return NoContent();
    }

    [HttpPost("{doc_id}/finalize")]
    [Authorize(Roles = "HR_Manager")]
    public async Task<IActionResult> FinalizeDocument(Guid doc_id)
    {
        await _documentService.FinalizeDocumentAsync(doc_id);
        return NoContent();
    }

    [HttpPost("{doc_id}/send-email")]
    [Authorize(Roles = "HR_Manager")]
    public async Task<IActionResult> SendEmail(Guid doc_id, [FromBody] SendEmailRequest request)
    {
        await _documentService.SendEmailAsync(doc_id, request);
        return NoContent();
    }
}
