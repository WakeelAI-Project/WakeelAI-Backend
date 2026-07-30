using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wakeel.Application.DTOs.Employees;
using Wakeel.Application.Interfaces;

namespace Wakeel.API.Controllers;

[ApiController]
[Route("api/employees")]
public class EmployeesController(IEmployeeService employeeService) : ControllerBase
{
    [Authorize(Roles = "HR_Manager")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateEmployeeRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(new ApiErrorResponse { Error = "validation_error", Message = "Invalid payload.", Status = 400 });

        var companyIdClaim = User.FindFirst("company_id")?.Value;
        var userIdClaim = User.FindFirst("user_id")?.Value;
        if (!Guid.TryParse(companyIdClaim, out var companyId) || !Guid.TryParse(userIdClaim, out var userId))
            return Forbid();

        try
        {
            var result = await employeeService.CreateEmployeeAsync(userId, companyId, request, cancellationToken);
            return Created(string.Empty, result);
        }
        catch (InvalidOperationException ex) when (ex.Message == "email_already_exists")
        {
            return Conflict(new ApiErrorResponse { Error = "email_already_exists", Message = "Email already exists.", Status = 409 });
        }
    }

    [Authorize(Roles = "HR_Manager,Company_Owner")]
    [HttpPatch("{recordId:guid}")]
    public async Task<IActionResult> Update([FromRoute] Guid recordId, [FromBody] UpdateEmployeeRequest request, CancellationToken cancellationToken)
    {
        var companyIdClaim = User.FindFirst("company_id")?.Value;
        if (!Guid.TryParse(companyIdClaim, out var companyId))
            return Forbid();

        var updated = await employeeService.UpdateEmployeeAsync(companyId, recordId, request, cancellationToken);
        if (updated is null)
            return NotFound(new { error = "employee_not_found" });

        return Ok(updated);
    }

    [Authorize(Roles = "HR_Manager,Company_Owner")]
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int limit = 20, CancellationToken cancellationToken = default)
    {
        var companyIdClaim = User.FindFirst("company_id")?.Value;
        if (!Guid.TryParse(companyIdClaim, out var companyId))
            return Forbid();

        var list = await employeeService.ListEmployeesAsync(companyId, status, page, limit, cancellationToken);
        return Ok(list);
    }

    [Authorize(Roles = "HR_Manager,Company_Owner")]
    [HttpGet("{recordId:guid}")]
    public async Task<IActionResult> Get([FromRoute] Guid recordId, CancellationToken cancellationToken)
    {
        var companyIdClaim = User.FindFirst("company_id")?.Value;
        if (!Guid.TryParse(companyIdClaim, out var companyId))
            return Forbid();

        var detail = await employeeService.GetEmployeeAsync(companyId, recordId, cancellationToken);
        if (detail is null)
            return NotFound(new { error = "employee_not_found" });

        return Ok(detail);
    }
}
