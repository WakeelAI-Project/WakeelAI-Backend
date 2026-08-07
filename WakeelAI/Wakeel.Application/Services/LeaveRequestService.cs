using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wakeel.Application.DTOs.LeaveRequests;
using Wakeel.Application.Interfaces.Repositories;
using Wakeel.Application.Interfaces.Services;
using Wakeel.Domain.Entities;

namespace Wakeel.Application.Services;

public class LeaveRequestService : ILeaveRequestService
{
    private readonly IUnitOfWork _unitOfWork;

    public LeaveRequestService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<LeaveRequestDto> CreateDraftAsync(Guid employeeId, Guid companyId, CreateLeaveRequestDto dto, bool hasAttachment, CancellationToken cancellationToken = default)
    {
        if (!DateOnly.TryParse(dto.StartDate, out var startDate) || !DateOnly.TryParse(dto.EndDate, out var endDate))
            throw new InvalidOperationException("validation_error");

        if (startDate < DateOnly.FromDateTime(DateTime.UtcNow) || endDate < startDate)
            throw new InvalidOperationException("validation_error");

        var daysRequested = endDate.DayNumber - startDate.DayNumber + 1; // Inclusive calendar days

        // Check sufficient balance at creation if Annual or Sick
        if (dto.LeaveType == "Annual" || dto.LeaveType == "Sick")
        {
            var year = startDate.Year;
            var balance = await _unitOfWork.LeaveBalances.FirstOrDefaultAsync(lb => lb.EmployeeId == employeeId && lb.LeaveType == dto.LeaveType && lb.Year == year, cancellationToken);
            
            if (balance == null || (balance.TotalDays.HasValue && balance.TotalDays.Value - balance.UsedDays < daysRequested))
            {
                throw new InvalidOperationException("insufficient_leave_balance");
            }
        }

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

        if (hasAttachment)
        {
            leaveRequest.AttachmentUrl = $"https://storage.wakeel-ai.com/attachments/{leaveRequest.Id}.pdf";
        }
        else if (dto.LeaveType == "Sick")
        {
            throw new InvalidOperationException("attachment_required");
        }

        await _unitOfWork.LeaveRequests.AddAsync(leaveRequest, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(leaveRequest, null); // Emloyee name can be null upon creation since it's an immediate return
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
}
