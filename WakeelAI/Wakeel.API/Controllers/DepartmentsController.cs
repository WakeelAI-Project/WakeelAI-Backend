using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wakeel.Application.DTOs.Departments;
using Wakeel.Application.Interfaces;

namespace Wakeel.API.Controllers;

/// <summary>
/// API controller for department management.
/// Provides endpoints for CRUD operations on departments within a company.
/// </summary>
[ApiController]
[Route("api/departments")]
public class DepartmentsController(IDepartmentService departmentService) : ControllerBase
{
    /// <summary>
    /// Creates a new department for the authenticated user's company.
    /// Requires Company_Owner role.
    /// </summary>
    /// <param name="request">The department creation request.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>201 Created with the new department data.</returns>
    [Authorize(Roles = "Company_Owner")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDepartmentRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(new ApiErrorResponse
            {
                Error = "validation_error",
                Message = "Invalid payload.",
                Status = 400
            });

        var companyIdClaim = User.FindFirst("company_id")?.Value;
        if (!Guid.TryParse(companyIdClaim, out var companyId))
            return Forbid();

        var result = await departmentService.CreateAsync(companyId, request, cancellationToken);
        return Created(string.Empty, result);
    }

    /// <summary>
    /// Lists all departments for the authenticated user's company with pagination.
    /// Requires Company_Owner or HR_Manager role.
    /// </summary>
    /// <param name="page">The page number (default: 1).</param>
    /// <param name="limit">The number of items per page (default: 20, max: 100).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>200 OK with a paginated list of departments.</returns>
    [Authorize(Roles = "Company_Owner,HR_Manager")]
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int limit = 20, CancellationToken cancellationToken = default)
    {
        var companyIdClaim = User.FindFirst("company_id")?.Value;
        if (!Guid.TryParse(companyIdClaim, out var companyId))
            return Forbid();

        var list = await departmentService.ListAsync(companyId, page, limit, cancellationToken);
        return Ok(list);
    }

    /// <summary>
    /// Gets a single department by ID.
    /// Requires Company_Owner or HR_Manager role.
    /// </summary>
    /// <param name="departmentId">The department ID to retrieve.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>200 OK with the department data, or 404 Not Found.</returns>
    [Authorize(Roles = "Company_Owner,HR_Manager")]
    [HttpGet("{departmentId:guid}")]
    public async Task<IActionResult> Get([FromRoute] Guid departmentId, CancellationToken cancellationToken)
    {
        var companyIdClaim = User.FindFirst("company_id")?.Value;
        if (!Guid.TryParse(companyIdClaim, out var companyId))
            return Forbid();

        var department = await departmentService.GetAsync(companyId, departmentId, cancellationToken);
        if (department is null)
            return NotFound(new ApiErrorResponse
            {
                Error = "department_not_found",
                Message = "Department not found.",
                Status = 404
            });

        return Ok(department);
    }

    /// <summary>
    /// Updates an existing department.
    /// Requires Company_Owner role.
    /// </summary>
    /// <param name="departmentId">The department ID to update.</param>
    /// <param name="request">The department update request.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>200 OK with the updated department data, or 404 Not Found.</returns>
    [Authorize(Roles = "Company_Owner")]
    [HttpPatch("{departmentId:guid}")]
    public async Task<IActionResult> Update([FromRoute] Guid departmentId, [FromBody] UpdateDepartmentRequest request, CancellationToken cancellationToken)
    {
        var companyIdClaim = User.FindFirst("company_id")?.Value;
        if (!Guid.TryParse(companyIdClaim, out var companyId))
            return Forbid();

        var updated = await departmentService.UpdateAsync(companyId, departmentId, request, cancellationToken);
        if (updated is null)
            return NotFound(new ApiErrorResponse
            {
                Error = "department_not_found",
                Message = "Department not found.",
                Status = 404
            });

        return Ok(updated);
    }

    /// <summary>
    /// Deletes a department (soft delete).
    /// Requires Company_Owner role.
    /// </summary>
    /// <param name="departmentId">The department ID to delete.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>204 No Content on success, 404 Not Found or 409 Conflict on failure.</returns>
    [Authorize(Roles = "Company_Owner")]
    [HttpDelete("{departmentId:guid}")]
    public async Task<IActionResult> Delete([FromRoute] Guid departmentId, CancellationToken cancellationToken)
    {
        var companyIdClaim = User.FindFirst("company_id")?.Value;
        if (!Guid.TryParse(companyIdClaim, out var companyId))
            return Forbid();

        var (success, errorCode) = await departmentService.DeleteAsync(companyId, departmentId, cancellationToken);

        if (!success)
        {
            return errorCode switch
            {
                "department_in_use" => Conflict(new ApiErrorResponse
                {
                    Error = "department_in_use",
                    Message = "Department has assigned employees and cannot be deleted.",
                    Status = 409
                }),
                _ => NotFound(new ApiErrorResponse
                {
                    Error = "department_not_found",
                    Message = "Department not found.",
                    Status = 404
                })
            };
        }

        return NoContent();
    }
}
