using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wakeel.Application.DTOs.AiIntegrations;
using Wakeel.Application.Interfaces;
using Wakeel.Application.Interfaces.Repositories;
using Wakeel.Application.Interfaces.Services;
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
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILeaveBalanceProvisioningService _leaveBalanceProvisioningService;
    private readonly IEmployeeService _employeeService;

    public InternalAiContextController(
        ApplicationDbContext dbContext,
        IUnitOfWork unitOfWork,
        ILeaveBalanceProvisioningService leaveBalanceProvisioningService,
        IEmployeeService employeeService)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _leaveBalanceProvisioningService = leaveBalanceProvisioningService;
        _employeeService = employeeService;
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
            .FirstOrDefaultAsync(p => p.UserId == userId && p.User.CompanyId == companyId, cancellationToken);

        if (profile == null)
        {
            return NotFound(new { error = new { code = "leave_request_not_found", message = "Employee not found." } });
        }

        // Provision on read and filter by the CURRENT year - matching EmployeeService's own
        // GetEmployeeAsync. Without a year filter, once a second year of balances exists this
        // endpoint and the mobile Home screen would report different numbers for the same
        // employee for whichever row LINQ happened to return first.
        var currentYear = DateTime.UtcNow.Year;
        await _leaveBalanceProvisioningService.EnsureYearAsync(userId, currentYear, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var balances = await _dbContext.LeaveBalances
            .Where(lb => lb.EmployeeId == userId && lb.Year == currentYear)
            .ToListAsync(cancellationToken);

        var annualBalance = balances.FirstOrDefault(b => b.LeaveType == "Annual");
        var sickBalance = balances.FirstOrDefault(b => b.LeaveType == "Sick");
        var unpaidBalance = balances.FirstOrDefault(b => b.LeaveType == "Unpaid");

        var response = new EmployeeContextResponse
        {
            UserId = userId.ToString(),
            CompanyId = companyId.ToString(),
            FullName = profile.User.FullName,
            Role = role,
            Department = profile.Department?.Name,
            JobTitle = profile.JobTitle,
            EmploymentStatus = profile.User.IsActive ? "Active" : "Inactive",
            Salary = profile.Salary,
            HireDate = profile.HireDate.ToString("yyyy-MM-dd"),
            LeaveBalance = new EmployeeLeaveBalancesDto
            {
                // Annual always carries a real cap, so ?? 0 is safe here - it never
                // silently hides a genuinely uncapped balance the way it would for
                // Sick/Unpaid, which report their true null (no cap) instead.
                Annual = annualBalance != null ? new LeaveBalanceContextDto
                {
                    TotalDays = annualBalance.TotalDays ?? 0,
                    UsedDays = annualBalance.UsedDays,
                    RemainingDays = (annualBalance.TotalDays ?? 0) - annualBalance.UsedDays,
                    IsUncapped = false
                } : null,
                Sick = sickBalance != null ? new LeaveBalanceContextDto
                {
                    TotalDays = sickBalance.TotalDays,
                    UsedDays = sickBalance.UsedDays,
                    RemainingDays = sickBalance.TotalDays.HasValue ? sickBalance.TotalDays.Value - sickBalance.UsedDays : null,
                    IsUncapped = !sickBalance.TotalDays.HasValue
                } : null,
                Unpaid = unpaidBalance != null ? new LeaveBalanceContextDto
                {
                    TotalDays = unpaidBalance.TotalDays,
                    UsedDays = unpaidBalance.UsedDays,
                    RemainingDays = unpaidBalance.TotalDays.HasValue ? unpaidBalance.TotalDays.Value - unpaidBalance.UsedDays : null,
                    IsUncapped = !unpaidBalance.TotalDays.HasValue
                } : null
            }
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
            TaxId = string.IsNullOrEmpty(company.TaxId) ? null : company.TaxId,
            Industry = string.IsNullOrEmpty(company.Industry) ? null : company.Industry,
            Address = string.IsNullOrEmpty(company.Address) ? null : company.Address,
            PhoneNumber = string.IsNullOrEmpty(company.PhoneNumber) ? null : company.PhoneNumber,
            Email = string.IsNullOrEmpty(company.Email) ? null : company.Email,
            LogoUrl = string.IsNullOrEmpty(company.LogoUrl) ? null : company.LogoUrl,
            WorkingHours = string.IsNullOrEmpty(company.WorkingHours) ? null : company.WorkingHours,
            RegisteredAt = company.RegisteredAt,
            PolicyAvailable = policyAvailable
        };

        return Ok(response);
    }

    /// <summary>
    /// FIX-17: lets the AI resolve an employee typed by name (e.g. from the general
    /// Assistant page, where no <c>targetEmployeeId</c> is set yet) instead of failing
    /// outright. HR_Manager-only, same as the document-generation skill that calls it.
    /// </summary>
    [HttpGet("employees/search")]
    public async Task<IActionResult> SearchEmployees([FromQuery] string name, CancellationToken cancellationToken)
    {
        var companyId = GetXCompanyId();
        var role = GetXRole();

        if (role != "HR_Manager")
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return Ok(new EmployeeSearchResponse());
        }

        var result = await _employeeService.ListEmployeesAsync(companyId, status: null, search: name, page: 1, limit: 5, cancellationToken);

        var response = new EmployeeSearchResponse
        {
            Employees = result.Data
                .Select(e => new EmployeeSearchResultDto { EmployeeId = e.UserId.ToString(), FullName = e.FullName })
                .ToList()
        };

        return Ok(response);
    }
}
