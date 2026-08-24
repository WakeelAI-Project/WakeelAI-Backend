using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wakeel.Application.DTOs.Employees;
using Wakeel.Application.Interfaces;
using Wakeel.Application.Interfaces.Repositories;
using Wakeel.Application.Interfaces.Services;
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
    private readonly ILeaveBalanceProvisioningService _leaveBalanceProvisioningService;
    private readonly IAuditLogService _auditLogService;

    public EmployeeService(
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        ILogger<EmployeeService> logger,
        IEmailSender emailSender,
        IResourceLoader resourceLoader,
        ILeaveBalanceProvisioningService leaveBalanceProvisioningService,
        IAuditLogService auditLogService)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _emailSender = emailSender ?? throw new ArgumentNullException(nameof(emailSender));
        _resourceLoader = resourceLoader ?? throw new ArgumentNullException(nameof(resourceLoader));
        _leaveBalanceProvisioningService = leaveBalanceProvisioningService ?? throw new ArgumentNullException(nameof(leaveBalanceProvisioningService));
        _auditLogService = auditLogService ?? throw new ArgumentNullException(nameof(auditLogService));
    }

    /// <summary>Writes an audit entry without letting a failure affect the caller's result.</summary>
    private async Task TryLogAuditAsync(Guid? userId, string action, string details)
    {
        try
        {
            await _auditLogService.LogActionAsync(userId, action, details);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write {Action} audit entry.", action);
        }
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

        // The provisioning service resolves an employee's HireDate by querying
        // EmployeeProfiles, which - unlike the change tracker - only sees rows that have
        // already been persisted. Save now so the profile just added above is visible to
        // that lookup, then provision the hire year's balances and save again.
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _leaveBalanceProvisioningService.EnsureYearAsync(profile.UserId, profile.HireDate.Year, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await TryLogAuditAsync(actorUserId, "EMPLOYEE_CREATED", $"Created employee \"{user.FullName}\" ({user.Email}).");

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

        // Same reservation LeaveRequestService's own validation enforces - a displayed
        // balance that omitted it used to tell an employee they had days a new request
        // would then reject as insufficient.
        var reservedAnnual = await _leaveBalanceProvisioningService.GetReservedDaysAsync(profile.UserId, "Annual", currentYear, cancellationToken);
        var reservedSick = await _leaveBalanceProvisioningService.GetReservedDaysAsync(profile.UserId, "Sick", currentYear, cancellationToken);
        var reservedUnpaid = await _leaveBalanceProvisioningService.GetReservedDaysAsync(profile.UserId, "Unpaid", currentYear, cancellationToken);

        var today = ResolveEmployeeToday(profile.TimeZoneId);
        var activeLeave = await _unitOfWork.LeaveRequests.FirstOrDefaultAsync(
            lr => lr.EmployeeId == profile.UserId && lr.Status == "Approved" && lr.StartDate <= today && lr.EndDate >= today,
            cancellationToken);

        return new EmployeeDetailResponse
        {
            RecordId = profile.UserId,
            UserId = profile.UserId,
            FullName = user.FullName,
            Email = user.Email,
            PhotoUrl = user.PhotoUrl,
            JobTitle = profile.JobTitle,
            DepartmentId = profile.DepartmentId,
            Department = department?.Name,
            NationalId = profile.NationalId,
            HireDate = profile.HireDate,
            Salary = profile.Salary,
            ContractType = profile.ContractType,
            EmploymentStatus = GetEmploymentStatus(user.IsActive),
            TimeZoneId = profile.TimeZoneId,
            LeaveBalance = new LeaveBalanceSummary
            {
                Annual = MapLeaveBalance(balances, "Annual", reservedAnnual),
                Sick = MapLeaveBalance(balances, "Sick", reservedSick),
                Unpaid = MapLeaveBalance(balances, "Unpaid", reservedUnpaid)
            },
            CurrentLeave = MapCurrentLeave(activeLeave, today)
        };
    }

    public async Task<EmployeeDetailResponse?> UpdatePhotoAsync(Guid companyId, Guid userId, string photoUrl, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken);
        if (user is null || user.CompanyId != companyId)
            return null;

        user.PhotoUrl = photoUrl;
        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetEmployeeAsync(companyId, userId, cancellationToken);
    }

    public async Task<EmployeeDetailResponse?> RemovePhotoAsync(Guid companyId, Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken);
        if (user is null || user.CompanyId != companyId)
            return null;

        user.PhotoUrl = null;
        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetEmployeeAsync(companyId, userId, cancellationToken);
    }

    public async Task<EmployeeDetailResponse?> UpdateTimeZoneAsync(Guid companyId, Guid userId, string timeZoneId, CancellationToken cancellationToken = default)
    {
        if (!IsValidTimeZoneId(timeZoneId))
            throw new InvalidOperationException("invalid_timezone");

        var profile = await _resourceLoader.GetEmployeeProfileAsync(userId, cancellationToken);
        if (profile is null)
            return null;

        var user = await _resourceLoader.GetUserAsync(profile.UserId, cancellationToken);
        if (user is null || user.CompanyId != companyId)
            return null;

        profile.TimeZoneId = timeZoneId;
        _unitOfWork.EmployeeProfiles.Update(profile);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetEmployeeAsync(companyId, userId, cancellationToken);
    }

    /// <summary>
    /// HR manual override of a leave balance's cap for one year. This is the mechanism for
    /// the two statutory Annual tiers this system does not compute automatically (age 50+,
    /// disability - see LeaveBalanceProvisioningService), and for any other one-off HR
    /// adjustment.
    /// </summary>
    public async Task<EmployeeDetailResponse?> AdjustLeaveBalanceAsync(Guid companyId, Guid actorUserId, Guid recordId, string leaveType, AdjustLeaveBalanceRequest request, CancellationToken cancellationToken = default)
    {
        var profile = await _resourceLoader.GetEmployeeProfileAsync(recordId, cancellationToken);
        if (profile is null)
            return null;

        var user = await _resourceLoader.GetUserAsync(profile.UserId, cancellationToken);
        if (user is null || user.CompanyId != companyId)
            return null;

        var balance = await _leaveBalanceProvisioningService.GetOrCreateAsync(recordId, leaveType, request.Year, cancellationToken);

        if (request.TotalDays.HasValue && request.TotalDays.Value < balance.UsedDays)
            throw new InvalidOperationException("validation_error");

        balance.TotalDays = request.TotalDays;
        _unitOfWork.LeaveBalances.Update(balance);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await TryLogAuditAsync(
            actorUserId,
            "LEAVE_BALANCE_ADJUSTED",
            $"HR set {leaveType} leave balance for {user.FullName} ({request.Year}) to " +
            (request.TotalDays.HasValue ? $"{request.TotalDays.Value} days" : "uncapped"));

        return await GetEmployeeAsync(companyId, recordId, cancellationToken);
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

    public async Task<EmployeeDetailResponse?> UpdateEmployeeAsync(Guid companyId, Guid actorUserId, Guid recordId, UpdateEmployeeRequest request, CancellationToken cancellationToken = default)
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

        await TryLogAuditAsync(actorUserId, "EMPLOYEE_UPDATED", $"Updated employee \"{user.FullName}\".");

        department ??= await _unitOfWork.Departments.GetByIdAsync(profile.DepartmentId, cancellationToken);

        return new EmployeeDetailResponse
        {
            RecordId = profile.UserId,
            UserId = profile.UserId,
            FullName = user.FullName,
            Email = user.Email,
            PhotoUrl = user.PhotoUrl,
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

    public async Task<bool> DeactivateEmployeeAsync(Guid companyId, Guid actorUserId, Guid recordId, CancellationToken cancellationToken = default)
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

        await TryLogAuditAsync(actorUserId, "EMPLOYEE_DELETED", $"Deactivated employee \"{user.FullName}\".");

        return true;
    }

    /// <summary>
    /// FIX-25: a data subject's own personal-data bundle. Authorization (HR_Manager or
    /// the employee themselves) is enforced by the controller; this method only enforces
    /// the multi-tenant scope, exactly like <see cref="GetEmployeeAsync"/>.
    /// </summary>
    public async Task<PersonalDataExportResponse?> ExportPersonalDataAsync(Guid companyId, Guid actorUserId, Guid recordId, CancellationToken cancellationToken = default)
    {
        var profile = await _resourceLoader.GetEmployeeProfileAsync(recordId, cancellationToken);
        if (profile is null)
            return null;

        var user = await _resourceLoader.GetUserAsync(profile.UserId, cancellationToken);
        if (user is null || user.CompanyId != companyId)
            return null;

        var department = await _unitOfWork.Departments.GetByIdAsync(profile.DepartmentId, cancellationToken);

        var balances = await _unitOfWork.LeaveBalances.FindAsync(
            lb => lb.EmployeeId == recordId, cancellationToken);

        var leaveRequests = await _unitOfWork.LeaveRequests.FindAsync(
            lr => lr.EmployeeId == recordId, cancellationToken);

        var documents = await _unitOfWork.GeneratedDocuments.FindAsync(
            d => d.EmployeeId == recordId, cancellationToken);

        var export = new PersonalDataExportResponse
        {
            ExportedAt = DateTime.UtcNow,
            User = new PersonalDataExportUser
            {
                UserId = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                Phone = user.Phone,
                Role = user.Role.ToString(),
                EmploymentStatus = GetEmploymentStatus(user.IsActive),
                CreatedAt = user.CreatedAt
            },
            Profile = new PersonalDataExportProfile
            {
                JobTitle = profile.JobTitle,
                Department = department?.Name,
                NationalId = profile.NationalId,
                HireDate = profile.HireDate,
                Salary = profile.Salary,
                ContractType = profile.ContractType,
                TimeZoneId = profile.TimeZoneId
            },
            LeaveBalances = balances
                .OrderByDescending(b => b.Year).ThenBy(b => b.LeaveType)
                .Select(b => new PersonalDataExportLeaveBalance
                {
                    LeaveType = b.LeaveType,
                    Year = b.Year,
                    TotalDays = b.TotalDays,
                    UsedDays = b.UsedDays
                }).ToList(),
            LeaveRequests = leaveRequests
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new PersonalDataExportLeaveRequest
                {
                    RequestId = r.Id,
                    LeaveType = r.LeaveType,
                    StartDate = r.StartDate,
                    EndDate = r.EndDate,
                    DaysRequested = r.DaysRequested,
                    Status = r.Status,
                    Reason = r.Reason,
                    CreatedAt = r.CreatedAt
                }).ToList(),
            GeneratedDocuments = documents
                .OrderByDescending(d => d.CreatedAt)
                .Select(d => new PersonalDataExportDocument
                {
                    DocumentId = d.Id,
                    DocumentType = d.DocumentType,
                    Title = d.Title,
                    Status = d.Status,
                    CreatedAt = d.CreatedAt
                }).ToList()
        };

        await TryLogAuditAsync(actorUserId, "PERSONAL_DATA_EXPORTED", $"Exported personal data for \"{user.FullName}\".");

        return export;
    }

    private async Task<Department> ValidateDepartmentAsync(Guid companyId, Guid departmentId, CancellationToken cancellationToken)
    {
        var department = await _unitOfWork.Departments.GetByIdAsync(departmentId, cancellationToken);
        if (department is null || department.IsDeleted || department.CompanyId != companyId)
            throw new InvalidOperationException("department_not_found");

        return department;
    }

    private static string GetEmploymentStatus(bool isActive) => isActive ? "Active" : "Inactive";

    private static LeaveTypeBalance? MapLeaveBalance(IEnumerable<LeaveBalance> balances, string leaveType, int reservedDays)
    {
        var match = balances.FirstOrDefault(b => string.Equals(b.LeaveType, leaveType, StringComparison.OrdinalIgnoreCase));
        if (match is null)
            return null;

        return new LeaveTypeBalance
        {
            TotalDays = match.TotalDays,
            UsedDays = match.UsedDays,
            RemainingDays = match.TotalDays.HasValue ? match.TotalDays.Value - match.UsedDays - reservedDays : null,
            ReservedDays = reservedDays
        };
    }

    private static CurrentLeaveInfo? MapCurrentLeave(LeaveRequest? request, DateOnly today)
    {
        if (request is null)
            return null;

        return new CurrentLeaveInfo
        {
            LeaveType = request.LeaveType,
            StartDate = request.StartDate.ToString("yyyy-MM-dd"),
            EndDate = request.EndDate.ToString("yyyy-MM-dd"),
            TotalDays = request.DaysRequested,
            ElapsedDays = today.DayNumber - request.StartDate.DayNumber + 1
        };
    }

    private static bool IsInFuture(DateTime? date) =>
        date.HasValue && DateOnly.FromDateTime(date.Value) > DateOnly.FromDateTime(DateTime.UtcNow);

    /// Resolves "today" for date-sensitive calculations (currently only
    /// CurrentLeave's ElapsedDays) in the employee's own time zone rather
    /// than UTC, so the day boundary lands at their local midnight instead
    /// of UTC midnight. Falls back to UTC if the employee has no synced
    /// time zone yet, or if the stored value somehow isn't a valid IANA id
    /// (e.g. edited directly in the database) — this must never throw.
    private static DateOnly ResolveEmployeeToday(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
            return DateOnly.FromDateTime(DateTime.UtcNow);

        try
        {
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
            return DateOnly.FromDateTime(localNow);
        }
        catch (TimeZoneNotFoundException)
        {
            return DateOnly.FromDateTime(DateTime.UtcNow);
        }
        catch (InvalidTimeZoneException)
        {
            return DateOnly.FromDateTime(DateTime.UtcNow);
        }
    }

    private static bool IsValidTimeZoneId(string timeZoneId)
    {
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }
}
