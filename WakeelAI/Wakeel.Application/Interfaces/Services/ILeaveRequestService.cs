using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wakeel.Application.DTOs.LeaveRequests;
using Wakeel.Domain.Entities;

namespace Wakeel.Application.Interfaces.Services;

public interface ILeaveRequestService
{
    /// <summary>
    /// Creates a leave request draft from a multipart form upload (used by the JWT employee endpoint).
    /// For Sick leave, an attachment stream must be provided.
    /// </summary>
    Task<LeaveRequestDto> CreateDraftAsync(Guid employeeId, Guid companyId, CreateLeaveRequestDto dto, (System.IO.Stream Stream, string FileName)? attachment, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a leave request draft from a JSON payload with a pre-existing attachment URL
    /// (used by the internal M2M AI endpoint). For Sick leave, dto.AttachmentUrl must be set.
    /// Identity is trusted from the caller — do not re-validate employee_id from the body.
    /// </summary>
    Task<LeaveRequestDto> CreateDraftFromUrlAsync(Guid employeeId, Guid companyId, InternalCreateLeaveRequestDto dto, CancellationToken cancellationToken = default);

    Task<(IEnumerable<LeaveRequestDto> Data, int Page, int Total)> ListAsync(Guid companyId, Guid? employeeId, string role, string? status, int page, int limit, CancellationToken cancellationToken = default);
    Task<LeaveRequestDto> GetByIdAsync(Guid requestId, Guid companyId, Guid? employeeId, string role, CancellationToken cancellationToken = default);
    Task<LeaveRequestDto> SubmitDraftAsync(Guid requestId, Guid employeeId, Guid companyId, CancellationToken cancellationToken = default);
    Task CancelDraftAsync(Guid requestId, Guid employeeId, Guid companyId, CancellationToken cancellationToken = default);
    Task<LeaveRequestDto> ReviewLeaveRequestAsync(Guid requestId, Guid companyId, Guid hrUserId, ReviewLeaveRequestDto dto, CancellationToken cancellationToken = default);
}

