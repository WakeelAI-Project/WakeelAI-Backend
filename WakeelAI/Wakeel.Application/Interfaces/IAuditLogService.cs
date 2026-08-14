using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Wakeel.Application.DTOs.AuditLogs;

namespace Wakeel.Application.Interfaces;

public interface IAuditLogService
{
    Task<(IEnumerable<AuditLogDto> Data, int Total)> GetAuditLogsAsync(int page, int limit, string? action, Guid? userId);
    
    Task LogActionAsync(Guid? userId, string action, string details);
}
