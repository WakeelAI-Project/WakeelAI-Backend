using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wakeel.Application.DTOs.Employees;
using Wakeel.Application.Interfaces;
using Wakeel.Application.Interfaces.Repositories;
using Wakeel.Domain.Entities;
using Wakeel.Domain.Enums;

namespace Wakeel.Application.Services;

public class EmployeeService : IEmployeeService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<EmployeeService> _logger;
    private readonly IEmailSender _emailSender;
    private readonly IResourceLoader _resourceLoader;

    public EmployeeService(IUnitOfWork unitOfWork, IPasswordHasher passwordHasher, ILogger<EmployeeService> logger, IEmailSender emailSender, IResourceLoader resourceLoader)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _emailSender = emailSender ?? throw new ArgumentNullException(nameof(emailSender));
        _resourceLoader = resourceLoader ?? throw new ArgumentNullException(nameof(resourceLoader));
    }

    public async Task<CreateEmployeeResponse> CreateEmployeeAsync(Guid actorUserId, Guid companyId, CreateEmployeeRequest request, CancellationToken cancellationToken = default)
    {
        if (IsInFuture(request.HireDate))
            throw new InvalidOperationException("hire_date_in_future");

        var department = await ValidateDepartmentAsync(companyId, request.DepartmentId!.Value, cancellationToken);

        // Ensure email uniqueness
        var emailExists = await _unitOfWork.Users.EmailExistsAsync(request.Email, cancellationToken);
        if (emailExists)
            throw new InvalidOperationException("email_already_exists");

        var tempPassword = Guid.NewGuid().ToString("N");
        var hashed = _passwordHasher.HashPassword(tempPassword);

        var user = new User
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Email = request.Email,
            PasswordHash = hashed,
            FullName = request.FullName,
            Phone = string.Empty,
            Role = UserRole.Employee,
            IsActive = true,
            IsEmailConfirmed = false,
            MustChangePassword = true,
            ActivationToken = string.Empty,
            ActivationTokenExpiry = DateTime.UtcNow,
            CreatedByUserId = actorUserId,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Users.AddAsync(user, cancellationToken);

        var profile = new EmployeeProfile
        {
            UserId = user.Id,
            DepartmentId = department.Id,
            JobTitle = request.JobTitle,
            Salary = request.Salary,
            HireDate = DateOnly.FromDateTime(request.HireDate),
            NationalId = request.NationalId,
            ContractType = request.ContractType
        };

        await _unitOfWork.EmployeeProfiles.AddAsync(profile, cancellationToken);

        var currentYear = DateTime.UtcNow.Year;
        var leaveBalances = new[]
        {
            new LeaveBalance { Id = Guid.NewGuid(), EmployeeId = profile.UserId, LeaveType = "Annual", TotalDays = 15, UsedDays = 0, Year = currentYear },
            new LeaveBalance { Id = Guid.NewGuid(), EmployeeId = profile.UserId, LeaveType = "Sick", TotalDays = 10, UsedDays = 0, Year = currentYear },
            new LeaveBalance { Id = Guid.NewGuid(), EmployeeId = profile.UserId, LeaveType = "Unpaid", TotalDays = null, UsedDays = 0, Year = currentYear }
        };

        foreach (var leaveBalance in leaveBalances)
            await _unitOfWork.LeaveBalances.AddAsync(leaveBalance, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // send email with credentials
        var subject = "You're added to Wakeel as an employee";
        var body = $"<p>Hello {user.FullName},</p><p>Your account has been created. Login: <strong>{user.Email}</strong> and temporary password: <strong>{tempPassword}</strong>. Please change your password after first login.</p>";
        try
        {
            await _emailSender.SendEmailAsync(user.Email, subject, body, cancellationToken);
        }
        catch
        {
            _logger.LogWarning("Failed to send employee email to {Email}", user.Email);
        }

        return new CreateEmployeeResponse
        {
            UserId = user.Id,
            RecordId = profile.UserId,
            FullName = user.FullName,
            Email = user.Email,
            JobTitle = profile.JobTitle,
            DepartmentId = profile.DepartmentId,
            HireDate = profile.HireDate,
            Salary = profile.Salary,
            ContractType = profile.ContractType,
            NationalId = profile.NationalId,
            EmploymentStatus = GetEmploymentStatus(user.IsActive)
        };
    }

    public async Task<EmployeeDetailResponse?> GetEmployeeAsync(Guid companyId, Guid recordId, CancellationToken cancellationToken = default)
    {
        var profile = await _resourceLoader.GetEmployeeProfileAsync(recordId, cancellationToken);
        if (profile is null)
            return null;

        var user = await _resourceLoader.GetUserAsync(profile.UserId, cancellationToken);
        if (user is null || user.CompanyId != companyId)
            return null;

        var department = await _unitOfWork.Departments.GetByIdAsync(profile.DepartmentId, cancellationToken);

        var currentYear = DateTime.UtcNow.Year;
        var balances = await _unitOfWork.LeaveBalances.FindAsync(
            lb => lb.EmployeeId == profile.UserId && lb.Year == currentYear, cancellationToken);

        return new EmployeeDetailResponse
        {
            RecordId = profile.UserId,
            UserId = profile.UserId,
            FullName = user.FullName,
            Email = user.Email,
            JobTitle = profile.JobTitle,
            DepartmentId = profile.DepartmentId,
            Department = department?.Name,
            NationalId = profile.NationalId,
            HireDate = profile.HireDate,
            Salary = profile.Salary,
            ContractType = profile.ContractType,
            EmploymentStatus = GetEmploymentStatus(user.IsActive),
            LeaveBalance = new LeaveBalanceSummary
            {
                Annual = MapLeaveBalance(balances, "Annual"),
                Sick = MapLeaveBalance(balances, "Sick"),
                Unpaid = MapLeaveBalance(balances, "Unpaid")
            }
        };
    }

    public async Task<EmployeeListResponse> ListEmployeesAsync(Guid companyId, string? status, string? search, int page, int limit, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        limit = Math.Clamp(limit, 1, 100);

        var profiles = await _unitOfWork.EmployeeProfiles.GetAllAsync(cancellationToken);
        var departmentNamesById = (await _unitOfWork.Departments.GetAllAsync(cancellationToken))
            .ToDictionary(d => d.Id, d => d.Name);
        var users = await _unitOfWork.Users.GetAllAsync(cancellationToken);

        // Join profiles with users and include email for search matching
        var joinedRaw = from p in profiles
                        join u in users on p.UserId equals u.Id
                        where u.CompanyId == companyId
                        select new
                        {
                            Profile = p,
                            User = u,
                            DepartmentName = departmentNamesById.GetValueOrDefault(p.DepartmentId)
                        };

        // Apply search if provided (matches full name or email, case-insensitive, trimmed)
        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim();
            joinedRaw = joinedRaw.Where(x =>
                (!string.IsNullOrEmpty(x.User.FullName) && x.User.FullName.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(x.User.Email) && x.User.Email.Contains(q, StringComparison.OrdinalIgnoreCase))
            );
        }

        var joined = joinedRaw.Select(x => new EmployeeListItem
        {
            RecordId = x.Profile.UserId,
            UserId = x.User.Id,
            FullName = x.User.FullName,
            JobTitle = x.Profile.JobTitle,
            Department = x.DepartmentName,
            EmploymentStatus = GetEmploymentStatus(x.User.IsActive)
        });

        if (string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase))
            joined = joined.Where(item => item.EmploymentStatus == "Active");
        else if (string.Equals(status, "Inactive", StringComparison.OrdinalIgnoreCase))
            joined = joined.Where(item => item.EmploymentStatus == "Inactive");

        var list = joined.ToList();
        var total = list.Count;
        var items = list.Skip((page - 1) * limit).Take(limit).ToList();

        return new EmployeeListResponse
        {
            Data = items,
            Page = page,
            Total = total
        };
    }

    public async Task<EmployeeDetailResponse?> UpdateEmployeeAsync(Guid companyId, Guid recordId, UpdateEmployeeRequest request, CancellationToken cancellationToken = default)
    {
        var profile = await _resourceLoader.GetEmployeeProfileAsync(recordId, cancellationToken);
        if (profile is null)
            return null;

        var user = await _resourceLoader.GetUserAsync(profile.UserId, cancellationToken);
        if (user is null || user.CompanyId != companyId)
            return null;

        if (IsInFuture(request.HireDate))
            throw new InvalidOperationException("hire_date_in_future");

        Department? department = null;
        if (request.DepartmentId.HasValue)
        {
            department = await ValidateDepartmentAsync(companyId, request.DepartmentId.Value, cancellationToken);
            profile.DepartmentId = department.Id;
        }

        if (!string.IsNullOrWhiteSpace(request.FullName))
            user.FullName = request.FullName!;
        if (!string.IsNullOrWhiteSpace(request.JobTitle))
            profile.JobTitle = request.JobTitle!;
        if (request.HireDate.HasValue)
            profile.HireDate = DateOnly.FromDateTime(request.HireDate.Value);
        if (request.Salary.HasValue)
            profile.Salary = request.Salary.Value;
        if (!string.IsNullOrWhiteSpace(request.ContractType))
            profile.ContractType = request.ContractType!;
        if (request.NationalId is not null)
            profile.NationalId = request.NationalId;

        _unitOfWork.Users.Update(user);
        _unitOfWork.EmployeeProfiles.Update(profile);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        department ??= await _unitOfWork.Departments.GetByIdAsync(profile.DepartmentId, cancellationToken);

        return new EmployeeDetailResponse
        {
            RecordId = profile.UserId,
            UserId = profile.UserId,
            FullName = user.FullName,
            Email = user.Email,
            JobTitle = profile.JobTitle,
            DepartmentId = profile.DepartmentId,
            Department = department?.Name,
            NationalId = profile.NationalId,
            HireDate = profile.HireDate,
            Salary = profile.Salary,
            ContractType = profile.ContractType,
            EmploymentStatus = GetEmploymentStatus(user.IsActive)
        };
    }

    public async Task<bool> DeactivateEmployeeAsync(Guid companyId, Guid recordId, CancellationToken cancellationToken = default)
    {
        var profile = await _resourceLoader.GetEmployeeProfileAsync(recordId, cancellationToken);
        if (profile is null)
            return false;

        var user = await _resourceLoader.GetUserAsync(profile.UserId, cancellationToken);
        if (user is null || user.CompanyId != companyId)
            return false;

        if (!user.IsActive)
            return true;

        user.IsActive = false;
        _unitOfWork.Users.Update(user);

        var activeTokens = await _unitOfWork.RefreshTokens.FindAsync(
            rt => rt.UserId == user.Id && !rt.IsRevoked, cancellationToken);
        foreach (var token in activeTokens)
        {
            token.IsRevoked = true;
            _unitOfWork.RefreshTokens.Update(token);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<Department> ValidateDepartmentAsync(Guid companyId, Guid departmentId, CancellationToken cancellationToken)
    {
        var department = await _unitOfWork.Departments.GetByIdAsync(departmentId, cancellationToken);
        if (department is null || department.IsDeleted || department.CompanyId != companyId)
            throw new InvalidOperationException("department_not_found");

        return department;
    }

    private static string GetEmploymentStatus(bool isActive) => isActive ? "Active" : "Inactive";

    private static LeaveTypeBalance? MapLeaveBalance(IEnumerable<LeaveBalance> balances, string leaveType)
    {
        var match = balances.FirstOrDefault(b => string.Equals(b.LeaveType, leaveType, StringComparison.OrdinalIgnoreCase));
        if (match is null)
            return null;

        return new LeaveTypeBalance
        {
            TotalDays = match.TotalDays,
            UsedDays = match.UsedDays,
            RemainingDays = match.TotalDays.HasValue ? match.TotalDays.Value - match.UsedDays : null
        };
    }

    private static bool IsInFuture(DateTime? date) =>
        date.HasValue && DateOnly.FromDateTime(date.Value) > DateOnly.FromDateTime(DateTime.UtcNow);
}
