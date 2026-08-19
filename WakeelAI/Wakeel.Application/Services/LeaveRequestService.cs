using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using Wakeel.Application.DTOs.LeaveRequests;
using Wakeel.Application.Interfaces;
using Wakeel.Application.Interfaces.Repositories;
using Wakeel.Application.Interfaces.Services;
using Wakeel.Domain.Entities;

namespace Wakeel.Application.Services;

public class LeaveRequestService : ILeaveRequestService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileService _fileService;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<LeaveRequestService> _logger;

    public LeaveRequestService(
        IUnitOfWork unitOfWork, 
        IFileService fileService, 
        IEmailSender emailSender, 
        ILogger<LeaveRequestService> logger)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
        _emailSender = emailSender ?? throw new ArgumentNullException(nameof(emailSender));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<LeaveRequestDto> CreateDraftAsync(Guid employeeId, Guid companyId, CreateLeaveRequestDto dto, (System.IO.Stream Stream, string FileName)? attachment, CancellationToken cancellationToken = default)
    {
        if (!DateOnly.TryParse(dto.StartDate, out var startDate) || !DateOnly.TryParse(dto.EndDate, out var endDate))
            throw new InvalidOperationException("validation_error");

        if (startDate < DateOnly.FromDateTime(DateTime.UtcNow) || endDate < startDate)
            throw new InvalidOperationException("validation_error");

        var daysRequested = endDate.DayNumber - startDate.DayNumber + 1; // Inclusive calendar days

        await ValidateNoOverlapAndBalanceAsync(employeeId, dto.LeaveType, startDate, endDate, daysRequested, cancellationToken);

        var leaveRequest = new LeaveRequest
        {
            Id = Guid.NewGuid(),
            EmployeeId = employeeId,
            CompanyId = companyId,
            LeaveType = dto.LeaveType,
            StartDate = startDate,
            EndDate = endDate,
            DaysRequested = daysRequested,
            Reason = dto.Reason,
            Status = "Draft",
            CreatedAt = DateTime.UtcNow
        };

        if (attachment.HasValue)
        {
            leaveRequest.AttachmentUrl = await _fileService.SaveFileAsync(attachment.Value.Stream, attachment.Value.FileName, "leave-requests", cancellationToken);
        }
        else if (dto.LeaveType == "Sick")
        {
            throw new InvalidOperationException("attachment_required");
        }

        await _unitOfWork.LeaveRequests.AddAsync(leaveRequest, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(leaveRequest, null); // Employee name can be null upon creation since it's an immediate return
    }

    /// <inheritdoc />
    public async Task<LeaveRequestDto> CreateDraftFromUrlAsync(
        Guid employeeId,
        Guid companyId,
        InternalCreateLeaveRequestDto dto,
        CancellationToken cancellationToken = default)
    {
        if (!DateOnly.TryParse(dto.StartDate, out var startDate) || !DateOnly.TryParse(dto.EndDate, out var endDate))
            throw new InvalidOperationException("validation_error");

        if (startDate < DateOnly.FromDateTime(DateTime.UtcNow) || endDate < startDate)
            throw new InvalidOperationException("validation_error");

        var daysRequested = endDate.DayNumber - startDate.DayNumber + 1;

        await ValidateNoOverlapAndBalanceAsync(employeeId, dto.LeaveType, startDate, endDate, daysRequested, cancellationToken);

        // For Sick leave, the attachment_url must be provided (pre-uploaded via POST /api/leave-requests/attachments)
        if (dto.LeaveType == "Sick" && string.IsNullOrWhiteSpace(dto.AttachmentUrl))
            throw new InvalidOperationException("attachment_required");

        var leaveRequest = new LeaveRequest
        {
            Id             = Guid.NewGuid(),
            EmployeeId     = employeeId,
            CompanyId      = companyId,
            LeaveType      = dto.LeaveType,
            StartDate      = startDate,
            EndDate        = endDate,
            DaysRequested  = daysRequested,
            Reason         = dto.Reason,
            Status         = "Draft",
            AttachmentUrl  = dto.AttachmentUrl,
            CreatedAt      = DateTime.UtcNow
        };

        await _unitOfWork.LeaveRequests.AddAsync(leaveRequest, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(leaveRequest, null);
    }

    public async Task<(IEnumerable<LeaveRequestDto> Data, int Page, int Total)> ListAsync(Guid companyId, Guid? employeeId, string role, string? status, int page, int limit, CancellationToken cancellationToken = default)
    {
        var requests = await _unitOfWork.LeaveRequests.FindAsync(lr => lr.CompanyId == companyId, cancellationToken);

        IEnumerable<LeaveRequest> query = requests;

        if (role == "Employee" && employeeId.HasValue)
        {
            query = query.Where(lr => lr.EmployeeId == employeeId.Value);
        }
        else if (role == "HR_Manager")
        {
            query = query.Where(lr => lr.Status != "Draft" && lr.Status != "Cancelled");
        }
        else
        {
            throw new UnauthorizedAccessException();
        }

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(lr => lr.Status == status);
        }

        var total = query.Count();
        
        var orderedRequests = query
            .OrderByDescending(lr => lr.CreatedAt)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToList();

        var userIds = orderedRequests.Select(r => r.EmployeeId).Distinct().ToList();
        var users = new Dictionary<Guid, string>();
        foreach (var id in userIds)
        {
            var u = await _unitOfWork.Users.GetByIdAsync(id, cancellationToken);
            if (u != null) users[id] = u.FullName;
        }

        var dtos = orderedRequests.Select(lr => MapToDto(lr, users.GetValueOrDefault(lr.EmployeeId)));

        return (dtos, page, total);
    }

    public async Task<LeaveRequestDto> GetByIdAsync(Guid requestId, Guid companyId, Guid? employeeId, string role, CancellationToken cancellationToken = default)
    {
        var request = await _unitOfWork.LeaveRequests.FirstOrDefaultAsync(lr => lr.Id == requestId && lr.CompanyId == companyId, cancellationToken);
        
        if (request == null)
        {
            throw new InvalidOperationException("leave_request_not_found");
        }

        if (role == "Employee" && employeeId.HasValue && request.EmployeeId != employeeId.Value)
        {
            throw new InvalidOperationException("leave_request_not_found");
        }
        else if (role == "HR_Manager" && (request.Status == "Draft" || request.Status == "Cancelled"))
        {
            throw new InvalidOperationException("leave_request_not_found");
        }
        else if (role != "Employee" && role != "HR_Manager")
        {
            throw new UnauthorizedAccessException();
        }

        var user = await _unitOfWork.Users.GetByIdAsync(request.EmployeeId, cancellationToken);
        return MapToDto(request, user?.FullName);
    }

    public async Task<LeaveRequestDto> SubmitDraftAsync(Guid requestId, Guid employeeId, Guid companyId, CancellationToken cancellationToken = default)
    {
        var request = await _unitOfWork.LeaveRequests.FirstOrDefaultAsync(lr => lr.Id == requestId && lr.EmployeeId == employeeId && lr.CompanyId == companyId, cancellationToken);

        if (request == null)
        {
            throw new InvalidOperationException("leave_request_not_found");
        }

        if (request.Status != "Draft")
        {
            throw new InvalidOperationException("not_a_draft");
        }

        request.Status = "Pending";
        request.SubmittedAt = DateTime.UtcNow;

        _unitOfWork.LeaveRequests.Update(request);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var user = await _unitOfWork.Users.GetByIdAsync(request.EmployeeId, cancellationToken);
        return MapToDto(request, user?.FullName);
    }

    public async Task CancelDraftAsync(Guid requestId, Guid employeeId, Guid companyId, CancellationToken cancellationToken = default)
    {
        var request = await _unitOfWork.LeaveRequests.FirstOrDefaultAsync(lr => lr.Id == requestId && lr.EmployeeId == employeeId && lr.CompanyId == companyId, cancellationToken);

        if (request == null)
        {
            throw new InvalidOperationException("leave_request_not_found");
        }

        if (request.Status != "Draft")
        {
            throw new InvalidOperationException("not_a_draft");
        }

        request.Status = "Cancelled";
        
        _unitOfWork.LeaveRequests.Update(request);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<LeaveRequestDto> ReviewLeaveRequestAsync(Guid requestId, Guid companyId, Guid hrUserId, ReviewLeaveRequestDto dto, CancellationToken cancellationToken = default)
    {
        var request = await _unitOfWork.LeaveRequests.FirstOrDefaultAsync(lr => lr.Id == requestId && lr.CompanyId == companyId, cancellationToken);

        if (request == null)
        {
            throw new InvalidOperationException("leave_request_not_found");
        }

        if (request.Status != "Pending")
        {
            throw new InvalidOperationException("not_pending");
        }

        if (dto.Status == "Approved")
        {
            var year = request.StartDate.Year;
            var balance = await _unitOfWork.LeaveBalances.FirstOrDefaultAsync(lb => lb.EmployeeId == request.EmployeeId && lb.LeaveType == request.LeaveType && lb.Year == year, cancellationToken);

            if (balance != null && balance.TotalDays.HasValue)
            {
                if (balance.TotalDays.Value - balance.UsedDays < request.DaysRequested)
                {
                    throw new InvalidOperationException("insufficient_leave_balance");
                }
                
                balance.UsedDays += request.DaysRequested;
                _unitOfWork.LeaveBalances.Update(balance);
            }
        }
        else if (dto.Status == "Rejected")
        {
            if (string.IsNullOrWhiteSpace(dto.HrNote))
            {
                throw new InvalidOperationException("validation_error");
            }
            request.HrNote = dto.HrNote;
        }

        request.Status = dto.Status;
        request.ReviewedByUserId = hrUserId;
        request.ReviewedAt = DateTime.UtcNow;

        _unitOfWork.LeaveRequests.Update(request);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var user = await _unitOfWork.Users.GetByIdAsync(request.EmployeeId, cancellationToken);
        
        if (user != null && !string.IsNullOrWhiteSpace(user.Email))
        {
            var subject = dto.Status == "Approved"
                ? "Your leave request has been approved"
                : "Your leave request has been rejected";
            var body =
                $"<p>Hello {user.FullName},</p>" +
                $"<p>Your {request.LeaveType} leave request from <strong>{request.StartDate:yyyy-MM-dd}</strong> " +
                $"to <strong>{request.EndDate:yyyy-MM-dd}</strong> ({request.DaysRequested} day(s)) has been " +
                $"<strong>{dto.Status}</strong>.</p>" +
                (string.IsNullOrWhiteSpace(request.HrNote) ? string.Empty : $"<p>HR note: {request.HrNote}</p>");
            try
            {
                await _emailSender.SendEmailAsync(user.Email, subject, body, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send leave request review email to {Email}", user.Email);
            }
        }

        return MapToDto(request, user?.FullName);
    }

    private static LeaveRequestDto MapToDto(LeaveRequest request, string? employeeName)
    {
        return new LeaveRequestDto
        {
            RequestId = request.Id,
            EmployeeId = request.EmployeeId,
            EmployeeName = employeeName,
            LeaveType = request.LeaveType,
            StartDate = request.StartDate.ToString("yyyy-MM-dd"),
            EndDate = request.EndDate.ToString("yyyy-MM-dd"),
            DaysRequested = request.DaysRequested,
            Status = request.Status,
            Reason = request.Reason,
            HrNote = request.HrNote,
            AttachmentUrl = request.AttachmentUrl,
            CreatedAt = request.CreatedAt,
            SubmittedAt = request.SubmittedAt,
            ReviewedAt = request.ReviewedAt
        };
    }

    private async Task ValidateNoOverlapAndBalanceAsync(
        Guid employeeId,
        string leaveType,
        DateOnly startDate,
        DateOnly endDate,
        int daysRequested,
        CancellationToken cancellationToken)
    {
        // 1) Reject any request overlapping an existing active request (any leave type).
        var overlapping = await _unitOfWork.LeaveRequests.FirstOrDefaultAsync(lr =>
            lr.EmployeeId == employeeId &&
            (lr.Status == "Draft" || lr.Status == "Pending" || lr.Status == "Approved") &&
            lr.StartDate <= endDate && lr.EndDate >= startDate,
            cancellationToken);

        if (overlapping != null)
            throw new InvalidOperationException("overlapping_leave_request");

        // 2) Balance check that also reserves days held by Draft/Pending requests of the same
        // type. Unpaid is included here too now that it carries a real (if often zero) cap.
        if (leaveType == "Annual" || leaveType == "Sick" || leaveType == "Unpaid")
        {
            var year = startDate.Year;
            var balance = await _unitOfWork.LeaveBalances.FirstOrDefaultAsync(
                lb => lb.EmployeeId == employeeId && lb.LeaveType == leaveType && lb.Year == year,
                cancellationToken);

            if (balance == null)
                throw new InvalidOperationException("insufficient_leave_balance");

            if (balance.TotalDays.HasValue)
            {
                var activeRequests = await _unitOfWork.LeaveRequests.FindAsync(lr =>
                    lr.EmployeeId == employeeId &&
                    lr.LeaveType == leaveType &&
                    (lr.Status == "Draft" || lr.Status == "Pending") &&
                    lr.StartDate.Year == year,
                    cancellationToken);

                var reservedDays = activeRequests.Sum(lr => lr.DaysRequested);
                var remaining = balance.TotalDays.Value - balance.UsedDays - reservedDays;
                if (remaining < daysRequested)
                    throw new InvalidOperationException("insufficient_leave_balance");
            }
        }
    }
}
