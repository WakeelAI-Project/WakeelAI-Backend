using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Wakeel.Application.DTOs.LeaveRequests;
using Wakeel.Application.Interfaces.Services;
using Wakeel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Wakeel.API.Controllers;

/// <summary>
/// Internal Machine-to-Machine (M2M) controller for leave request operations
/// initiated by the Node.js AI service on behalf of an employee.
///
/// Security: Secured exclusively via InternalApiKeyMiddleware (PSK). JWT is NOT used.
/// [AllowAnonymous] is required to bypass the global [Authorize] filter;
/// the PSK middleware handles authentication before this controller is reached.
///
/// Identity: Employee identity is extracted STRICTLY from the trusted X-User-Id and
/// X-Company-Id headers, validated upstream by InternalApiKeyMiddleware.
/// Any employee_id in the request body is ignored by design — the DTO has no such field.
/// </summary>
[ApiController]
[Route("api/ai/leave-requests")]
[AllowAnonymous] // PSK security is enforced by InternalApiKeyMiddleware
public class InternalAiLeaveController : ControllerBase
{
    private readonly ILeaveRequestService _leaveRequestService;
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<InternalAiLeaveController> _logger;

    /// <summary>
    /// Initializes a new instance of the InternalAiLeaveController.
    /// </summary>
    public InternalAiLeaveController(
        ILeaveRequestService leaveRequestService,
        ApplicationDbContext dbContext,
        ILogger<InternalAiLeaveController> logger)
    {
        _leaveRequestService = leaveRequestService;
        _dbContext           = dbContext;
        _logger              = logger;
    }

    // -------- Identity extraction from trusted M2M headers --------
    // Safe to call without null checks because InternalApiKeyMiddleware
    // has already validated and confirmed these headers are present and valid GUIDs.

    private Guid GetEmployeeId() => Guid.Parse(Request.Headers["X-User-Id"]!);
    private Guid GetCompanyId()  => Guid.Parse(Request.Headers["X-Company-Id"]!);
    private string GetRole()     => Request.Headers["X-Role"].ToString();

    // -------- GET /api/ai/leave-requests/latest-draft --------

    /// <summary>
    /// Returns the caller's own most recent Draft leave request, so the AI service can
    /// resolve which draft an ambiguous "send it" / "cancel it" chat message refers to
    /// when no id can be extracted from the message or the conversation history.
    /// Scoped strictly to the X-User-Id header - never allows enumerating another
    /// employee's drafts, even given a different X-Company-Id.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation.</param>
    /// <returns>200 OK with the draft's id, type, and dates, or 404 if none exists.</returns>
    [HttpGet("latest-draft")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLatestDraft(CancellationToken cancellationToken)
    {
        if (GetRole() != "Employee")
            return StatusCode(StatusCodes.Status403Forbidden, new ApiErrorResponse { Error = "forbidden", Message = "Only employees have leave drafts.", Status = 403 });

        var employeeId = GetEmployeeId();
        var companyId = GetCompanyId();

        var draft = await _dbContext.LeaveRequests
            .Where(lr => lr.EmployeeId == employeeId && lr.CompanyId == companyId && lr.Status == "Draft")
            .OrderByDescending(lr => lr.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (draft == null)
            return NotFound(new ApiErrorResponse { Error = "leave_request_not_found", Message = "No draft leave request found.", Status = 404 });

        return Ok(new
        {
            request_id = draft.Id,
            leave_type = draft.LeaveType,
            start_date = draft.StartDate.ToString("yyyy-MM-dd"),
            end_date = draft.EndDate.ToString("yyyy-MM-dd"),
            days_requested = draft.DaysRequested
        });
    }

    // -------- POST /api/ai/leave-requests --------

    /// <summary>
    /// Creates a leave request draft on behalf of an employee via the AI service.
    /// For Sick leave, attachment_url in the request body must contain the URL
    /// returned by POST /api/leave-requests/attachments (the employee pre-upload step).
    /// employee_id is never read from the body — only from the X-User-Id header.
    /// </summary>
    /// <param name="dto">The leave request details. Must NOT contain employee_id.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation.</param>
    /// <returns>201 Created with request_id, status, and days_requested.</returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateDraft(
        [FromBody] InternalCreateLeaveRequestDto dto,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(new ApiErrorResponse { Error = "validation_error", Message = "Invalid payload.", Status = 400 });

        if (GetRole() != "Employee")
            return StatusCode(StatusCodes.Status403Forbidden, new ApiErrorResponse { Error = "forbidden", Message = "Only employees can request leave.", Status = 403 });

        var employeeId = GetEmployeeId();
        var companyId  = GetCompanyId();

        _logger.LogInformation(
            "Internal AI leave draft: EmployeeId={EmployeeId}, CompanyId={CompanyId}, LeaveType={LeaveType}",
            employeeId, companyId, dto.LeaveType);

        if (!string.IsNullOrWhiteSpace(dto.AttachmentUrl))
        {
            // The AI intent router requires absolute URLs, but the DB stores relative URLs.
            // Extract the filename part and compare it to handle both cases securely.
            var incomingFilenameCleaned = dto.AttachmentUrl.Split('/', StringSplitOptions.RemoveEmptyEntries).Last().Replace(".", "").Replace("-", "");

            var validAttachment = await _dbContext.LeaveAttachments
                .FirstOrDefaultAsync(a => a.Url.Replace(".", "").Replace("-", "").EndsWith(incomingFilenameCleaned) && a.CompanyId == companyId, cancellationToken);
            
            if (validAttachment == null)
            {
                return UnprocessableEntity(new ApiErrorResponse { Error = "invalid_attachment", Message = "Invalid attachment URL or cross-company request.", Status = 422 });
            }
            
            // Pass the uncorrupted URL down to the service
            dto = dto with { AttachmentUrl = validAttachment.Url };
        }

        try
        {
            var result = await _leaveRequestService.CreateDraftFromUrlAsync(employeeId, companyId, dto, cancellationToken);
            return Created(string.Empty, new
            {
                request_id     = result.RequestId,
                status         = result.Status,
                days_requested = result.DaysRequested
            });
        }
        catch (InvalidOperationException ex) when (ex.Message == "validation_error")
        {
            return BadRequest(new ApiErrorResponse { Error = "validation_error", Message = "Invalid dates or unsupported leave type.", Status = 400 });
        }
        catch (InvalidOperationException ex) when (ex.Message == "attachment_required")
        {
            return UnprocessableEntity(new ApiErrorResponse { Error = "attachment_required", Message = "Medical report attachment URL is required for Sick leave. Upload via POST /api/leave-requests/attachments first.", Status = 422 });
        }
        catch (InvalidOperationException ex) when (ex.Message == "insufficient_leave_balance")
        {
            return UnprocessableEntity(new ApiErrorResponse { Error = "insufficient_leave_balance", Message = "Requested days exceed the employee's remaining leave balance.", Status = 422 });
        }
    }

    // -------- PATCH /api/ai/leave-requests/{request_id}/submit --------

    /// <summary>
    /// Submits a leave request draft (transitions it from Draft to Pending).
    /// The employee identity is read from X-User-Id header, not the route or body.
    /// </summary>
    /// <param name="requestId">The ID of the leave request to submit.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation.</param>
    /// <returns>200 OK with request_id and updated status.</returns>
    [HttpPatch("{requestId:guid}/submit")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SubmitDraft(
        [FromRoute] Guid requestId,
        CancellationToken cancellationToken)
    {
        if (GetRole() != "Employee")
            return StatusCode(StatusCodes.Status403Forbidden, new ApiErrorResponse { Error = "forbidden", Message = "Only employees can submit leave requests.", Status = 403 });

        var employeeId = GetEmployeeId();
        var companyId  = GetCompanyId();

        try
        {
            var result = await _leaveRequestService.SubmitDraftAsync(requestId, employeeId, companyId, cancellationToken);
            return Ok(new { request_id = result.RequestId, status = result.Status });
        }
        catch (InvalidOperationException ex) when (ex.Message == "leave_request_not_found")
        {
            return NotFound(new ApiErrorResponse { Error = "leave_request_not_found", Message = "Leave request not found or does not belong to this employee.", Status = 404 });
        }
        catch (InvalidOperationException ex) when (ex.Message == "not_a_draft")
        {
            return Conflict(new ApiErrorResponse { Error = "not_a_draft", Message = "Leave request has already been submitted.", Status = 409 });
        }
    }

    // -------- DELETE /api/ai/leave-requests/{request_id} --------

    /// <summary>
    /// Cancels a leave request draft (transitions it from Draft to Cancelled).
    /// The employee identity is read from X-User-Id header, not the route or body.
    /// </summary>
    /// <param name="requestId">The ID of the leave request draft to cancel.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpDelete("{requestId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelDraft(
        [FromRoute] Guid requestId,
        CancellationToken cancellationToken)
    {
        if (GetRole() != "Employee")
            return StatusCode(StatusCodes.Status403Forbidden, new ApiErrorResponse { Error = "forbidden", Message = "Only employees can cancel leave requests.", Status = 403 });

        var employeeId = GetEmployeeId();
        var companyId  = GetCompanyId();

        try
        {
            await _leaveRequestService.CancelDraftAsync(requestId, employeeId, companyId, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex) when (ex.Message == "leave_request_not_found")
        {
            return NotFound(new ApiErrorResponse { Error = "leave_request_not_found", Message = "Leave request not found or does not belong to this employee.", Status = 404 });
        }
        catch (InvalidOperationException ex) when (ex.Message == "not_a_draft")
        {
            return Conflict(new ApiErrorResponse { Error = "not_a_draft", Message = "Leave request has already been submitted and cannot be cancelled.", Status = 409 });
        }
    }
}
