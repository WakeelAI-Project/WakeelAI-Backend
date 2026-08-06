using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wakeel.Application.DTOs.Dashboard;
using Wakeel.Application.Interfaces;
using Wakeel.Application.Interfaces.Repositories;

namespace Wakeel.Application.Services;

public class DashboardService : IDashboardService
{
    private readonly IUnitOfWork _unitOfWork;

    public DashboardService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<DashboardSummaryResponse> GetSummaryAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var profiles = await _unitOfWork.EmployeeProfiles.GetAllAsync(cancellationToken);
        var users = await _unitOfWork.Users.GetAllAsync(cancellationToken);

        var companyUsersById = users
            .Where(u => u.CompanyId == companyId)
            .ToDictionary(u => u.Id);

        var companyProfiles = profiles
            .Where(p => companyUsersById.ContainsKey(p.UserId))
            .ToList();

        var activeEmployees = companyProfiles.Count(p => companyUsersById[p.UserId].IsActive);

        return new DashboardSummaryResponse
        {
            EmployeeCount = companyProfiles.Count,
            ActiveEmployees = activeEmployees,
            // No LeaveRequest / CompanyHandbook / GeneratedDocument entities exist yet.
            // These stay at placeholder values until those features are built.
            PendingLeaveRequests = 0,
            HandbookUploaded = false,
            GeneratedDocumentsCount = 0
        };
    }
}
