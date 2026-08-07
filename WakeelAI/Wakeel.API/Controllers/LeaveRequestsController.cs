using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wakeel.Application.DTOs.LeaveRequests;
using Wakeel.Application.Interfaces.Services;

namespace Wakeel.API.Controllers;

[ApiController]
[Route("api/leave-requests")]
[Authorize]
public class LeaveRequestsController : ControllerBase
{
    private readonly ILeaveRequestService _leaveRequestService;

    public LeaveRequestsController(ILeaveRequestService leaveRequestService)
    {
        _leaveRequestService = leaveRequestService;
    }

    private Guid GetCompanyId() => Guid.Parse(User.FindFirstValue("company_id") ?? throw new UnauthorizedAccessException());
    private Guid GetUserId() => Guid.Parse(User.FindFirstValue("user_id") ?? throw new UnauthorizedAccessException());
    private string GetRole() => User.FindFirstValue("role") ?? throw new UnauthorizedAccessException();

    [HttpPost]
    [Authorize(Roles = "Employee")]
    public async Task<IActionResult> CreateDraft(
        [FromForm] CreateLeaveRequestDto dto,
        IFormFile? attachment,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(new ApiErrorResponse { Error = "validation_error", Message = "Invalid payload.", Status = 400 });

        try
        {
            var companyId = GetCompanyId();
            var employeeId = GetUserId();
            (System.IO.Stream, string)? fileAttachment = attachment != null ? (attachment.OpenReadStream(), attachment.FileName) : null;

            var result = await _leaveRequestService.CreateDraftAsync(employeeId, companyId, dto, fileAttachment, cancellationToken);
            return Created("", new { request_id = result.RequestId, status = result.Status, days_requested = result.DaysRequested });
        }
        catch (InvalidOperationException ex) when (ex.Message == "validation_error")
        {
            return BadRequest(new ApiErrorResponse { Error = "validation_error", Message = "Invalid dates or unsupported role.", Status = 400 });
        }
        catch (InvalidOperationException ex) when (ex.Message == "insufficient_leave_balance")
        {
            return UnprocessableEntity(new ApiErrorResponse { Error = "insufficient_leave_balance", Message = "Requested days exceed remaining balance", Status = 422 });
        }
        catch (InvalidOperationException ex) when (ex.Message == "attachment_required")
        {
            return UnprocessableEntity(new ApiErrorResponse { Error = "attachment_required", Message = "Medical report attachment missing for sick leave", Status = 422 });
        }
    }

    [HttpGet]
    [Authorize(Roles = "Employee, HR_Manager")]
    public async Task<IActionResult> List(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var companyId = GetCompanyId();
        var userId = GetUserId();
        var role = GetRole();
        Guid? employeeId = role == "Employee" ? userId : null;

        var result = await _leaveRequestService.ListAsync(companyId, employeeId, role, status, page, limit, cancellationToken);
        return Ok(new { data = result.Data, page = result.Page, total = result.Total });
    }

    [HttpGet("{id}")]
    [Authorize(Roles = "Employee, HR_Manager")]
    public async Task<IActionResult> GetById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var companyId = GetCompanyId();
            var userId = GetUserId();
            var role = GetRole();
            Guid? employeeId = role == "Employee" ? userId : null;

            var result = await _leaveRequestService.GetByIdAsync(id, companyId, employeeId, role, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex) when (ex.Message == "leave_request_not_found")
        {
            return NotFound(new ApiErrorResponse { Error = "leave_request_not_found", Message = "Doesn't exist, belongs to another employee/company", Status = 404 });
        }
    }

    [HttpPatch("{id}/submit")]
    [Authorize(Roles = "Employee")]
    public async Task<IActionResult> SubmitDraft(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var companyId = GetCompanyId();
            var employeeId = GetUserId();

            var result = await _leaveRequestService.SubmitDraftAsync(id, employeeId, companyId, cancellationToken);
            return Ok(new { request_id = result.RequestId, status = result.Status });
        }
        catch (InvalidOperationException ex) when (ex.Message == "leave_request_not_found")
        {
            return NotFound(new ApiErrorResponse { Error = "leave_request_not_found", Message = "Doesn't exist, belongs to another employee/company", Status = 404 });
        }
        catch (InvalidOperationException ex) when (ex.Message == "not_a_draft")
        {
            return Conflict(new ApiErrorResponse { Error = "not_a_draft", Message = "Already submitted", Status = 409 });
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Employee")]
    public async Task<IActionResult> CancelDraft(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var companyId = GetCompanyId();
            var employeeId = GetUserId();

            await _leaveRequestService.CancelDraftAsync(id, employeeId, companyId, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex) when (ex.Message == "leave_request_not_found")
        {
            return NotFound(new ApiErrorResponse { Error = "leave_request_not_found", Message = "Doesn't exist, belongs to another employee/company", Status = 404 });
        }
        catch (InvalidOperationException ex) when (ex.Message == "not_a_draft")
        {
            return Conflict(new ApiErrorResponse { Error = "not_a_draft", Message = "Already submitted", Status = 409 });
        }
    }

    [HttpPatch("{id}")]
    [Authorize(Roles = "HR_Manager")]
    public async Task<IActionResult> ReviewRequest(
        [FromRoute] Guid id,
        [FromBody] ReviewLeaveRequestDto dto,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(new ApiErrorResponse { Error = "validation_error", Message = "Invalid payload.", Status = 400 });

        try
        {
            var companyId = GetCompanyId();
            var hrUserId = GetUserId();

            var result = await _leaveRequestService.ReviewLeaveRequestAsync(id, companyId, hrUserId, dto, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex) when (ex.Message == "validation_error")
        {
            return BadRequest(new ApiErrorResponse { Error = "validation_error", Message = "Invalid status, or rejection without hr_note", Status = 400 });
        }
        catch (InvalidOperationException ex) when (ex.Message == "leave_request_not_found")
        {
            return NotFound(new ApiErrorResponse { Error = "leave_request_not_found", Message = "Doesn't exist, belongs to another employee/company", Status = 404 });
        }
        catch (InvalidOperationException ex) when (ex.Message == "not_pending")
        {
            return Conflict(new ApiErrorResponse { Error = "not_pending", Message = "Request already reviewed or not yet submitted", Status = 409 });
        }
        catch (InvalidOperationException ex) when (ex.Message == "insufficient_leave_balance")
        {
            return UnprocessableEntity(new ApiErrorResponse { Error = "insufficient_leave_balance", Message = "Balance changed since submission; approval would overdraft", Status = 422 });
        }
    }
}
