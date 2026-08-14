using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Wakeel.Application.DTOs.AuditLogs;
using Wakeel.Application.Interfaces;
using Wakeel.Application.Interfaces.Repositories;
using Wakeel.Domain.Entities;

namespace Wakeel.Application.Services;

public class AuditLogService : IAuditLogService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentTenantService _currentTenantService;

    public AuditLogService(IUnitOfWork unitOfWork, ICurrentTenantService currentTenantService)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _currentTenantService = currentTenantService ?? throw new ArgumentNullException(nameof(currentTenantService));
    }

    public async Task<(IEnumerable<AuditLogDto> Data, int Total)> GetAuditLogsAsync(int page, int limit, string? action, Guid? userId)
    {
        var allLogs = await _unitOfWork.AuditLogs.GetAllAsync();
        var query = allLogs.AsQueryable();

        if (!string.IsNullOrEmpty(action))
            query = query.Where(a => a.Action == action);

        if (userId.HasValue)
            query = query.Where(a => a.UserId == userId.Value);

        var total = query.Count();

        var logs = query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * limit)
            .Take(limit)
            .Select(a => new AuditLogDto
            {
                Id = a.Id,
                Action = a.Action,
                Details = a.Details,
                UserId = a.UserId,
                CreatedAt = a.CreatedAt
            })
            .ToList();

        return (logs, total);
    }

    public async Task LogActionAsync(Guid? userId, string action, string details)
    {
        if (!_currentTenantService.HasTenant)
            return;

        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            CompanyId = _currentTenantService.CompanyId!.Value,
            UserId = userId,
            Action = action,
            Details = details,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.AuditLogs.AddAsync(auditLog);
        await _unitOfWork.SaveChangesAsync();
    }
}
