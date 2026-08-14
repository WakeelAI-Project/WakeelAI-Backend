using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wakeel.Application.Interfaces;

namespace Wakeel.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Company_Admin")]
public class AuditLogsController : ControllerBase
{
    private readonly IAuditLogService _auditLogService;

    public AuditLogsController(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService ?? throw new ArgumentNullException(nameof(auditLogService));
    }

    [HttpGet]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] string? action,
        [FromQuery] Guid? userId,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20)
    {
        var (logs, total) = await _auditLogService.GetAuditLogsAsync(page, limit, action, userId);

        return Ok(new
        {
            data = logs,
            page = page,
            limit = limit,
            total = total
        });
    }
}
