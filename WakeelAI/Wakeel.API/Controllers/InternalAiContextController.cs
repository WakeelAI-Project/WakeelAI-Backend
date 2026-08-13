using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wakeel.Application.DTOs.AiIntegrations;
using Wakeel.Infrastructure.Persistence;

namespace Wakeel.API.Controllers;

/// <summary>
/// Internal Machine-to-Machine (M2M) controller for fetching context data
/// (employee and company) required by the Node.js AI service.
/// Secured exclusively via InternalApiKeyMiddleware (PSK).
/// </summary>
[ApiController]
[Route("api/ai")]
[AllowAnonymous]
public class InternalAiContextController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;

    public InternalAiContextController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    private Guid GetXUserId() => Guid.Parse(Request.Headers["X-User-Id"]!);
    private Guid GetXCompanyId() => Guid.Parse(Request.Headers["X-Company-Id"]!);
    private string GetXRole() => Request.Headers["X-Role"]!;

    [HttpGet("employee-context")]
    public async Task<IActionResult> GetEmployeeContext(CancellationToken cancellationToken)
    {
        var userId = GetXUserId();
        var companyId = GetXCompanyId();
        var role = GetXRole();

        // The query filter automatically ensures we only query within the tenant (companyId).
        // For employee-context, we map the authenticated user's EmployeeProfile.
        var profile = await _dbContext.EmployeeProfiles
            .Include(p => p.User)
            .Include(p => p.Department)
            .Include(p => p.LeaveBalances)
            .FirstOrDefaultAsync(p => p.UserId == userId && p.User.CompanyId == companyId, cancellationToken);

        if (profile == null)
        {
            return NotFound(new { error = new { code = "leave_request_not_found", message = "Employee not found." } });
        }

        var annualBalance = profile.LeaveBalances.FirstOrDefault(b => b.LeaveType == "Annual");

        var response = new EmployeeContextResponse
        {
            UserId = userId.ToString(),
            CompanyId = companyId.ToString(),
            FullName = profile.User.FullName,
            Role = role,
            Department = profile.Department?.Name,
            EmploymentStatus = profile.User.IsActive ? "Active" : "Inactive",
            LeaveBalance = annualBalance != null ? new LeaveBalanceContextDto
            {
                Annual = annualBalance.TotalDays ?? 0,
                Used = annualBalance.UsedDays,
                Remaining = (annualBalance.TotalDays ?? 0) - annualBalance.UsedDays
            } : null
        };

        return Ok(response);
    }

    [HttpGet("company-context")]
    public async Task<IActionResult> GetCompanyContext(CancellationToken cancellationToken)
    {
        var companyId = GetXCompanyId();

        var company = await _dbContext.Companies
            .FirstOrDefaultAsync(c => c.Id == companyId, cancellationToken);

        if (company == null)
        {
            return NotFound();
        }

        var policyAvailable = await _dbContext.CompanyHandbooks
            .AnyAsync(h => h.CompanyId == companyId, cancellationToken);

        var response = new CompanyContextResponse
        {
            CompanyId = companyId.ToString(),
            CompanyName = company.Name,
            Industry = string.IsNullOrEmpty(company.Industry) ? null : company.Industry,
            WorkingHours = string.IsNullOrEmpty(company.WorkingHours) ? null : company.WorkingHours,
            PolicyAvailable = policyAvailable
        };

        return Ok(response);
    }
}
