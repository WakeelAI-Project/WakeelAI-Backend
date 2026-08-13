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
    private readonly ILogger<LeaveAttachmentController> _logger;

    private static readonly string[] AllowedExtensions = { ".pdf", ".jpg", ".jpeg", ".png" };
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    /// <summary>
    /// Initializes a new instance of the LeaveAttachmentController.
    /// </summary>
    public LeaveAttachmentController(IFileService fileService, ILogger<LeaveAttachmentController> logger)
    {
        _fileService = fileService;
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
        IFormFile? attachment,
        CancellationToken cancellationToken)
    {
        if (attachment == null || attachment.Length == 0)
            return BadRequest(new ApiErrorResponse
            {
                Error   = "validation_error",
                Message = "An attachment file is required.",
                Status  = 400
            });

        if (attachment.Length > MaxFileSizeBytes)
            return BadRequest(new ApiErrorResponse
            {
                Error   = "file_too_large",
                Message = "Attachment exceeds the 10 MB size limit.",
                Status  = 400
            });

        var extension = Path.GetExtension(attachment.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
            return BadRequest(new ApiErrorResponse
            {
                Error   = "invalid_file_type",
                Message = "Only PDF, JPG, and PNG files are allowed.",
                Status  = 400
            });

        using var stream = attachment.OpenReadStream();
        var attachmentUrl = await _fileService.SaveFileAsync(stream, attachment.FileName, "leave-requests", cancellationToken);

        _logger.LogInformation("Leave attachment uploaded: {Url}", attachmentUrl);

        return Created(string.Empty, new { attachment_url = attachmentUrl });
    }
}
