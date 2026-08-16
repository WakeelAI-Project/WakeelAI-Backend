using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Wakeel.Application.Interfaces;
using Wakeel.Domain.Entities;
using Wakeel.Infrastructure.Persistence;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace Wakeel.API.Controllers;

/// <summary>
/// Handles pre-upload of leave request attachments (e.g., medical reports for Sick leave).
/// The client calls this BEFORE initiating an AI chat leave flow.
/// The returned attachment_url is then passed by the Node.js AI service in the
/// body of POST /api/ai/leave-requests when creating a Sick leave draft.
/// </summary>
[ApiController]
[Route("api/leave-requests")]
[Authorize]
public class LeaveAttachmentController : ControllerBase
{
    private readonly IFileService _fileService;
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<LeaveAttachmentController> _logger;

    private static readonly string[] AllowedExtensions = { ".pdf", ".jpg", ".jpeg", ".png" };
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    /// <summary>
    /// Initializes a new instance of the LeaveAttachmentController.
    /// </summary>
    public LeaveAttachmentController(IFileService fileService, ApplicationDbContext dbContext, ILogger<LeaveAttachmentController> logger)
    {
        _fileService = fileService;
        _dbContext   = dbContext;
        _logger      = logger;
    }

    /// <summary>
    /// Uploads a leave request attachment (medical report, etc.) and returns
    /// the URL to be included in a subsequent leave request draft creation.
    /// Only Employees may upload leave attachments.
    /// </summary>
    /// <param name="attachment">The file to upload (PDF, JPG, or PNG, max 10 MB).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation.</param>
    /// <returns>201 Created with the attachment_url.</returns>
    [HttpPost("attachments")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UploadAttachment(
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new ApiErrorResponse
            {
                Error   = "validation_error",
                Message = "An attachment file is required.",
                Status  = 400
            });

        if (file.Length > MaxFileSizeBytes)
            return BadRequest(new ApiErrorResponse
            {
                Error   = "invalid_attachment",
                Message = "Attachment exceeds the 10 MB size limit.",
                Status  = 400
            });

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
            return BadRequest(new ApiErrorResponse
            {
                Error   = "invalid_attachment",
                Message = "Only PDF, JPG, and PNG files are allowed.",
                Status  = 400
            });

        using var stream = file.OpenReadStream();
        var attachmentUrl = await _fileService.SaveFileAsync(stream, file.FileName, "leave-requests", cancellationToken);

        Guid userId;
        Guid companyId;

        // If the request is authenticated via JWT, prefer claims. Otherwise accept explicit headers
        // from the mobile app: X-User-Id and X-Company-Id (public mode).
        if (User?.Identity != null && User.Identity.IsAuthenticated)
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value ?? User.FindFirst("role")?.Value;
            if (!string.Equals(role, "Employee", StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            var userClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("user_id")?.Value;
            var companyClaim = User.FindFirst("company_id")?.Value;
            if (!Guid.TryParse(userClaim, out userId) || !Guid.TryParse(companyClaim, out companyId))
            {
                return BadRequest(new ApiErrorResponse { Error = "validation_error", Message = "Invalid user or company claims.", Status = 400 });
            }
        }
        else
        {
            // Public upload mode - mobile app must supply X-User-Id and X-Company-Id headers.
            if (!Request.Headers.TryGetValue("X-User-Id", out var headerUser) || !Request.Headers.TryGetValue("X-Company-Id", out var headerCompany))
            {
                return BadRequest(new ApiErrorResponse { Error = "missing_identity_headers", Message = "X-User-Id and X-Company-Id headers are required for anonymous uploads.", Status = 400 });
            }

            if (!Guid.TryParse(headerUser.ToString(), out userId) || !Guid.TryParse(headerCompany.ToString(), out companyId))
            {
                return BadRequest(new ApiErrorResponse { Error = "validation_error", Message = "Invalid X-User-Id or X-Company-Id format.", Status = 400 });
            }
        }

        var attachmentRecord = new LeaveAttachment
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            EmployeeId = userId,
            Url = attachmentUrl,
            CreatedAt = DateTime.UtcNow
        };

        // Validate the referenced employee exists and belongs to the given company.
        var employeeExists = await _dbContext.Users.AnyAsync(u => u.Id == userId && u.CompanyId == companyId, cancellationToken);
        if (!employeeExists)
        {
            return NotFound(new ApiErrorResponse { Error = "employee_not_found", Message = "Employee not found.", Status = 404 });
        }

        _dbContext.LeaveAttachments.Add(attachmentRecord);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Leave attachment uploaded: {Url} with ID: {Id}", attachmentUrl, attachmentRecord.Id);

        return Created(string.Empty, new { attachment_id = attachmentRecord.Id.ToString(), url = attachmentUrl });
    }
}
