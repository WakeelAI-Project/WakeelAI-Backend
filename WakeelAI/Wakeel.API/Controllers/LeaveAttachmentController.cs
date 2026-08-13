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
    [Authorize(Roles = "Employee")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
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

        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("user_id")!);
        var companyId = Guid.Parse(User.FindFirstValue("company_id")!);

        var attachmentRecord = new LeaveAttachment
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            EmployeeId = userId,
            Url = attachmentUrl,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.LeaveAttachments.Add(attachmentRecord);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Leave attachment uploaded: {Url} with ID: {Id}", attachmentUrl, attachmentRecord.Id);

        return Created(string.Empty, new { attachment_id = attachmentRecord.Id.ToString(), url = attachmentUrl });
    }
}
