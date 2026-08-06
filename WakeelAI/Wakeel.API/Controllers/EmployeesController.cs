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
        catch (InvalidOperationException ex) when (ex.Message == "hire_date_in_future")
        {
            return BadRequest(new ApiErrorResponse { Error = "hire_date_in_future", Message = "Hire date cannot be in the future.", Status = 400 });
        }
        catch (InvalidOperationException ex) when (ex.Message == "department_not_found")
        {
            return NotFound(new ApiErrorResponse { Error = "department_not_found", Message = "Department not found.", Status = 404 });
        }
    }

    [Authorize(Roles = "HR_Manager,Company_Owner")]
    [HttpPatch("{recordId:guid}")]
    public async Task<IActionResult> Update([FromRoute] Guid recordId, [FromBody] UpdateEmployeeRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(new ApiErrorResponse { Error = "validation_error", Message = "Invalid payload.", Status = 400 });

        var companyIdClaim = User.FindFirst("company_id")?.Value;
        if (!Guid.TryParse(companyIdClaim, out var companyId))
            return Forbid();

        try
        {
            var updated = await employeeService.UpdateEmployeeAsync(companyId, recordId, request, cancellationToken);
            if (updated is null)
                return NotFound(new ApiErrorResponse { Error = "employee_not_found", Message = "Employee not found.", Status = 404 });

            return Ok(updated);
        }
        catch (InvalidOperationException ex) when (ex.Message == "hire_date_in_future")
        {
            return BadRequest(new ApiErrorResponse { Error = "hire_date_in_future", Message = "Hire date cannot be in the future.", Status = 400 });
        }
        catch (InvalidOperationException ex) when (ex.Message == "department_not_found")
        {
            return NotFound(new ApiErrorResponse { Error = "department_not_found", Message = "Department not found.", Status = 404 });
        }
    }

    [Authorize(Roles = "HR_Manager")]
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int limit = 20, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(status) &&
            !string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(status, "Inactive", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new ApiErrorResponse { Error = "validation_error", Message = "status must be Active or Inactive.", Status = 400 });
        }

        var companyIdClaim = User.FindFirst("company_id")?.Value;
        if (!Guid.TryParse(companyIdClaim, out var companyId))
            return Forbid();

        var list = await employeeService.ListEmployeesAsync(companyId, status, page, limit, cancellationToken);
        return Ok(list);
    }

    [Authorize(Roles = "HR_Manager")]
    [HttpGet("{recordId:guid}")]
    public async Task<IActionResult> Get([FromRoute] Guid recordId, CancellationToken cancellationToken)
    {
        var companyIdClaim = User.FindFirst("company_id")?.Value;
        if (!Guid.TryParse(companyIdClaim, out var companyId))
            return Forbid();

        var detail = await employeeService.GetEmployeeAsync(companyId, recordId, cancellationToken);
        if (detail is null)
            return NotFound(new ApiErrorResponse { Error = "employee_not_found", Message = "Employee not found.", Status = 404 });

        return Ok(detail);
    }

    [Authorize(Roles = "HR_Manager")]
    [HttpDelete("{recordId:guid}")]
    public async Task<IActionResult> Deactivate([FromRoute] Guid recordId, CancellationToken cancellationToken)
    {
        var companyIdClaim = User.FindFirst("company_id")?.Value;
        if (!Guid.TryParse(companyIdClaim, out var companyId))
            return Forbid();

        var deactivated = await employeeService.DeactivateEmployeeAsync(companyId, recordId, cancellationToken);
        if (!deactivated)
            return NotFound(new ApiErrorResponse { Error = "employee_not_found", Message = "Employee not found.", Status = 404 });

        return NoContent();
    }
}
