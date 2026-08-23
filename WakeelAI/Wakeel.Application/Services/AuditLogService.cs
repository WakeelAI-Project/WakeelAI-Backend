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

    public async Task<(IEnumerable<AuditLogDto> Data, int Total)> GetAuditLogsAsync(int page, int limit, string? action, Guid? userId, string? userName = null)
    {
        var allLogs = await _unitOfWork.AuditLogs.GetAllAsync();
        var query = allLogs.AsQueryable();

        if (!string.IsNullOrEmpty(action))
            query = query.Where(a => a.Action == action);

        if (userId.HasValue)
            query = query.Where(a => a.UserId == userId.Value);

        // userName filter: resolve matching user IDs then filter logs by those IDs
        if (!string.IsNullOrWhiteSpace(userName))
        {
            var trimmed = userName.Trim();
            var allUsers = await _unitOfWork.Users.GetAllAsync();
            var matchingIds = allUsers
                .Where(u => !string.IsNullOrEmpty(u.FullName) &&
                            u.FullName.Contains(trimmed, StringComparison.OrdinalIgnoreCase))
                .Select(u => u.Id)
                .ToHashSet();

            query = query.Where(a => a.UserId.HasValue && matchingIds.Contains(a.UserId.Value));
        }

        var total = query.Count();

        var pagedLogs = query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToList();

        // Resolve unique user IDs to names in a single batch lookup
        var distinctUserIds = pagedLogs
            .Where(a => a.UserId.HasValue)
            .Select(a => a.UserId!.Value)
            .Distinct()
            .ToList();

        var userNameMap = new Dictionary<Guid, string>();
        foreach (var uid in distinctUserIds)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(uid);
            if (user is not null && !string.IsNullOrWhiteSpace(user.FullName))
                userNameMap[uid] = user.FullName;
        }

        var logs = pagedLogs.Select(a => new AuditLogDto
        {
            Id = a.Id,
            Action = a.Action,
            Details = a.Details,
            UserId = a.UserId,
            UserName = a.UserId.HasValue && userNameMap.TryGetValue(a.UserId.Value, out var name) ? name : null,
            CreatedAt = a.CreatedAt
        }).ToList();

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
