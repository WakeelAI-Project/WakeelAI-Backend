using System;
using System.Threading;
using System.Threading.Tasks;
using Wakeel.Application.DTOs.Dashboard;

namespace Wakeel.Application.Interfaces;

public interface IDashboardService
{
    Task<DashboardSummaryResponse> GetSummaryAsync(Guid companyId, CancellationToken cancellationToken = default);
}
