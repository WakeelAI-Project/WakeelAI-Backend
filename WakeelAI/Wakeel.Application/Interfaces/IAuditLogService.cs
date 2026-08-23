using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Wakeel.Application.DTOs.AuditLogs;

namespace Wakeel.Application.Interfaces;

public interface IAuditLogService
{
    Task<(IEnumerable<AuditLogDto> Data, int Total)> GetAuditLogsAsync(int page, int limit, string? action, Guid? userId, string? userName = null);
    
    /// <param name="companyId">
    /// Overrides the ambient current-tenant company for this entry. Required for actions
    /// that happen before a tenant context exists on the request - e.g. company
    /// registration, where the company is being created in the same call.
    /// </param>
    Task LogActionAsync(Guid? userId, string action, string details, Guid? companyId = null);
}
