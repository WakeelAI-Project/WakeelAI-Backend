using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wakeel.Application.DTOs.LeaveRequests;
using Wakeel.Domain.Entities;

namespace Wakeel.Application.Interfaces.Services;

public interface ILeaveRequestService
{
    Task<LeaveRequestDto> CreateDraftAsync(Guid employeeId, Guid companyId, CreateLeaveRequestDto dto, bool hasAttachment, CancellationToken cancellationToken = default);
    Task<(IEnumerable<LeaveRequestDto> Data, int Page, int Total)> ListAsync(Guid companyId, Guid? employeeId, string role, string? status, int page, int limit, CancellationToken cancellationToken = default);
    Task<LeaveRequestDto> GetByIdAsync(Guid requestId, Guid companyId, Guid? employeeId, string role, CancellationToken cancellationToken = default);
    Task<LeaveRequestDto> SubmitDraftAsync(Guid requestId, Guid employeeId, Guid companyId, CancellationToken cancellationToken = default);
    Task CancelDraftAsync(Guid requestId, Guid employeeId, Guid companyId, CancellationToken cancellationToken = default);
    Task<LeaveRequestDto> ReviewLeaveRequestAsync(Guid requestId, Guid companyId, Guid hrUserId, ReviewLeaveRequestDto dto, CancellationToken cancellationToken = default);
}
