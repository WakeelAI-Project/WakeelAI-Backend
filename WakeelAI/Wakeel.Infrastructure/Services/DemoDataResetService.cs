using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wakeel.Application.Interfaces;
using Wakeel.Application.Interfaces.Repositories;
using Wakeel.Application.Interfaces.Services;
using Wakeel.Domain.Entities;
using Wakeel.Domain.Enums;
using Wakeel.Infrastructure.Persistence;

namespace Wakeel.Infrastructure.Services;

/// <inheritdoc cref="IDemoDataResetService" />
public class DemoDataResetService : IDemoDataResetService
{
    public const string DemoPassword = "Demo@12345";

    private static readonly Guid AnnualEntitlementId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SickEntitlementId = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid UnpaidEntitlementId = new("33333333-3333-3333-3333-333333333333");

    private readonly ApplicationDbContext _db;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILeaveBalanceProvisioningService _leaveBalanceProvisioningService;
    private readonly ILogger<DemoDataResetService> _logger;

    public DemoDataResetService(
        ApplicationDbContext db,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        ILeaveBalanceProvisioningService leaveBalanceProvisioningService,
        ILogger<DemoDataResetService> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        _leaveBalanceProvisioningService = leaveBalanceProvisioningService ?? throw new ArgumentNullException(nameof(leaveBalanceProvisioningService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<DemoDataResetResult> ResetAsync(CancellationToken cancellationToken = default)
    {
        await WipeAllDataAsync(cancellationToken);
        var logins = await SeedDemoDataAsync(cancellationToken);

        _logger.LogInformation(
            "Demo data reset complete: wiped all tenant data and seeded {Count} demo account(s).",
            logins.Count);

        return new DemoDataResetResult(logins);
    }

    /// <summary>
    /// Deletes every row from every table. Order matters: most FKs in
    /// Persistence/Configurations/*.cs are DeleteBehavior.Restrict, so a parent delete
    /// fails unless its children are gone first. Uses ExecuteDeleteAsync (bulk, bypasses
    /// the change tracker) with IgnoreQueryFilters so this removes data across every
    /// tenant, not just whichever company happens to be the current one (there isn't one -
    /// this only ever runs from a CLI flag, outside any HTTP request/tenant context).
    /// </summary>
    private async Task WipeAllDataAsync(CancellationToken cancellationToken)
    {
        await _db.RefreshTokens.ExecuteDeleteAsync(cancellationToken);
        await _db.PasswordResetOtps.ExecuteDeleteAsync(cancellationToken);
        await _db.LeaveAttachments.IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken);
        await _db.LeaveBalances.IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken);
        await _db.LeaveRequests.IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken);
        await _db.GeneratedDocuments.IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken);
        await _db.DocumentTemplates.IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken);
        await _db.CompanyHandbooks.IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken);
        await _db.AuditLogs.IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken);
        await _db.EmployeeProfiles.IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken);
        await _db.Users.IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken);
        await _db.Departments.IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken);
        await _db.Companies.ExecuteDeleteAsync(cancellationToken);
        await _db.LeaveEntitlements.ExecuteDeleteAsync(cancellationToken);

        // LEAVE_ENTITLEMENT is a shared policy lookup, not tenant data - deleting rows at
        // runtime doesn't undo LeaveEntitlementConfiguration's migration-time HasData seed,
        // and leave balance provisioning requires these three fixed-GUID rows to exist.
        _db.LeaveEntitlements.AddRange(
            new LeaveEntitlement
            {
                Id = AnnualEntitlementId,
                LeaveType = "Annual",
                BaseDays = 15,
                StandardDays = 21,
                SeniorDays = 30,
                SeniorityYears = 10,
                MinimumServiceMonths = 6
            },
            new LeaveEntitlement { Id = SickEntitlementId, LeaveType = "Sick", DefaultDays = null },
            new LeaveEntitlement { Id = UnpaidEntitlementId, LeaveType = "Unpaid", DefaultDays = null });

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<List<DemoLoginInfo>> SeedDemoDataAsync(CancellationToken cancellationToken)
    {
        var logins = new List<DemoLoginInfo>();
        var passwordHash = _passwordHasher.HashPassword(DemoPassword);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var currentYear = DateTime.UtcNow.Year;
        var phoneCounter = 1000000;

        var companySpecs = new[]
        {
            new DemoCompanySpec(
                Name: "Acme Manufacturing Co.",
                Slug: "acmemanufacturing",
                TaxId: "EG-TAX-100234",
                Industry: "Manufacturing",
                Address: "12 Industrial Zone, 6th of October City, Giza",
                Phone: "+20222345678",
                OwnerName: "Youssef Ibrahim",
                HrName: "Sara Adel",
                Employees:
                [
                    new DemoEmployeeSpec("Mahmoud Zaki", "Engineering", "Production Engineer"),
                    new DemoEmployeeSpec("Aya Mostafa", "Engineering", "Maintenance Technician"),
                    new DemoEmployeeSpec("Omar Nabil", "Human Resources", "HR Specialist"),
                    new DemoEmployeeSpec("Farida Samir", "Human Resources", "Recruitment Coordinator"),
                    new DemoEmployeeSpec("Hany Adel", "Sales", "Sales Executive"),
                    new DemoEmployeeSpec("Nourhan Tarek", "Sales", "Account Manager")
                ]),
            new DemoCompanySpec(
                Name: "Nile Software Solutions",
                Slug: "nilesoftware",
                TaxId: "EG-TAX-200781",
                Industry: "Technology",
                Address: "45 Tahrir Street, Downtown, Cairo",
                Phone: "+20233456789",
                OwnerName: "Mona Fathy",
                HrName: "Karim Hassan",
                Employees:
                [
                    new DemoEmployeeSpec("Ziad Elsayed", "Engineering", "Backend Developer"),
                    new DemoEmployeeSpec("Salma Reda", "Engineering", "Frontend Developer"),
                    new DemoEmployeeSpec("Marwan Fouad", "Human Resources", "HR Generalist"),
                    new DemoEmployeeSpec("Dina Khalil", "Human Resources", "People Operations Analyst"),
                    new DemoEmployeeSpec("Ahmed Sabry", "Sales", "Business Development Rep"),
                    new DemoEmployeeSpec("Yasmin Gaber", "Sales", "Client Success Manager")
                ])
        };

        var nationalId = 29000000000000L;

        foreach (var spec in companySpecs)
        {
            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = spec.Name,
                TaxId = spec.TaxId,
                Industry = spec.Industry,
                Address = spec.Address,
                RegisteredAt = DateTime.UtcNow.AddYears(-3),
                IsActive = true,
                PhoneNumber = spec.Phone,
                Email = $"info@{spec.Slug}.test",
                LogoUrl = string.Empty,
                WorkingHours = "9:00 AM - 5:00 PM"
            };
            await _unitOfWork.Companies.AddAsync(company, cancellationToken);

            var departmentsByName = new Dictionary<string, Department>();
            foreach (var deptName in new[] { "Engineering", "Human Resources", "Sales" })
            {
                var department = new Department
                {
                    Id = Guid.NewGuid(),
                    CompanyId = company.Id,
                    Name = deptName,
                    Description = $"{deptName} department",
                    IsDeleted = false,
                    CreatedAt = DateTime.UtcNow.AddYears(-3)
                };
                departmentsByName[deptName] = department;
                await _unitOfWork.Departments.AddAsync(department, cancellationToken);
            }

            var owner = NewUser(company.Id, spec.OwnerName, $"owner@{spec.Slug}.test", UserRole.Company_Owner, passwordHash, ref phoneCounter);
            var hr = NewUser(company.Id, spec.HrName, $"hr@{spec.Slug}.test", UserRole.HR_Manager, passwordHash, ref phoneCounter);
            await _unitOfWork.Users.AddAsync(owner, cancellationToken);
            await _unitOfWork.Users.AddAsync(hr, cancellationToken);

            logins.Add(new DemoLoginInfo(spec.Name, "Company_Owner", owner.Email, DemoPassword));
            logins.Add(new DemoLoginInfo(spec.Name, "HR_Manager", hr.Email, DemoPassword));

            // Owner/HR must be persisted before employees reference them (CreatedByUserId,
            // LeaveRequest.ReviewedByUserId), and department IDs must exist before profiles do.
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var employeeIndex = 0;
            foreach (var employeeSpec in spec.Employees)
            {
                employeeIndex++;
                var localPart = ToEmailLocalPart(employeeSpec.FullName);
                var user = NewUser(company.Id, employeeSpec.FullName, $"{localPart}@{spec.Slug}.test", UserRole.Employee, passwordHash, ref phoneCounter);
                user.CreatedByUserId = hr.Id;
                await _unitOfWork.Users.AddAsync(user, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                var yearsOfService = employeeIndex % 3 == 0 ? 0.4 : 2 + employeeIndex % 4;
                var hireDate = today.AddDays(-(int)(yearsOfService * 365));
                nationalId++;

                var profile = new EmployeeProfile
                {
                    UserId = user.Id,
                    DepartmentId = departmentsByName[employeeSpec.Department].Id,
                    JobTitle = employeeSpec.JobTitle,
                    Salary = 8000m + employeeIndex * 1500m,
                    HireDate = hireDate,
                    NationalId = nationalId.ToString(),
                    ContractType = "Full-Time"
                };
                await _unitOfWork.EmployeeProfiles.AddAsync(profile, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                logins.Add(new DemoLoginInfo(spec.Name, "Employee", user.Email, DemoPassword));

                // Reuses the app's own entitlement-computation logic (service-length tiers,
                // uncapped Sick/Unpaid) rather than reimplementing it here.
                await _leaveBalanceProvisioningService.EnsureYearAsync(user.Id, currentYear, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                var approvedDays = 3;
                var approvedStart = today.AddDays(-30);
                await _unitOfWork.LeaveRequests.AddAsync(new LeaveRequest
                {
                    Id = Guid.NewGuid(),
                    EmployeeId = user.Id,
                    CompanyId = company.Id,
                    LeaveType = "Annual",
                    StartDate = approvedStart,
                    EndDate = approvedStart.AddDays(approvedDays - 1),
                    DaysRequested = approvedDays,
                    Reason = "Family vacation",
                    Status = "Approved",
                    ReviewedByUserId = hr.Id,
                    CreatedAt = DateTime.UtcNow.AddDays(-32),
                    SubmittedAt = DateTime.UtcNow.AddDays(-32),
                    ReviewedAt = DateTime.UtcNow.AddDays(-31)
                }, cancellationToken);

                // Mirrors LeaveRequestService.ReviewAsync's own usage-increment on approval,
                // so the balance the demo shows is consistent with the request history.
                var annualBalance = await _leaveBalanceProvisioningService.GetOrCreateAsync(user.Id, "Annual", currentYear, cancellationToken);
                annualBalance.UsedDays += approvedDays;
                _unitOfWork.LeaveBalances.Update(annualBalance);

                var pendingStart = today.AddDays(2);
                await _unitOfWork.LeaveRequests.AddAsync(new LeaveRequest
                {
                    Id = Guid.NewGuid(),
                    EmployeeId = user.Id,
                    CompanyId = company.Id,
                    LeaveType = "Sick",
                    StartDate = pendingStart,
                    EndDate = pendingStart,
                    DaysRequested = 1,
                    Reason = "Doctor's appointment",
                    Status = "Pending",
                    CreatedAt = DateTime.UtcNow.AddHours(-6),
                    SubmittedAt = DateTime.UtcNow.AddHours(-6)
                }, cancellationToken);

                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            // Same three default templates AuthService seeds for every newly registered
            // company (see AuthService.BuildDefaultTemplates), so the demo looks like a
            // company that went through normal registration.
            var contractTemplate = new DocumentTemplate
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                DocumentType = "Contract",
                Name = "Default Employment Contract",
                ContentTemplate = "This Employment Contract is made on {{date}} between {{company_name}} and {{employee_name}}, " +
                    "who is hired as {{job_title}} in the {{department}} department under a {{contract_type}} contract, " +
                    "effective {{hire_date}}, with a monthly salary of {{salary}}.",
                IsActive = true
            };
            var warningTemplate = new DocumentTemplate
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                DocumentType = "Warning_Letter",
                Name = "Default Warning Letter",
                ContentTemplate = "Dear {{employee_name}},\n\nThis letter serves as a formal warning issued on {{date}} regarding your " +
                    "conduct as {{job_title}} in the {{department}} department at {{company_name}}. Please treat this matter with the " +
                    "seriousness it deserves.\n\nSincerely,\n{{company_name}} Management",
                IsActive = true
            };
            var terminationTemplate = new DocumentTemplate
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                DocumentType = "Termination_Letter",
                Name = "Default Termination Letter",
                ContentTemplate = "Dear {{employee_name}},\n\nThis letter confirms the termination of your employment as {{job_title}} " +
                    "in the {{department}} department at {{company_name}}, effective {{date}}. Your last working day and final " +
                    "settlement details will be communicated separately.\n\nSincerely,\n{{company_name}} Management",
                IsActive = true
            };
            await _unitOfWork.DocumentTemplates.AddAsync(contractTemplate, cancellationToken);
            await _unitOfWork.DocumentTemplates.AddAsync(warningTemplate, cancellationToken);
            await _unitOfWork.DocumentTemplates.AddAsync(terminationTemplate, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var firstEmployeeName = spec.Employees[0].FullName;
            await _unitOfWork.GeneratedDocuments.AddAsync(new GeneratedDocument
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                TemplateId = contractTemplate.Id,
                GeneratedByUserId = hr.Id,
                DocumentType = "Contract",
                Title = $"Employment Contract - {firstEmployeeName}",
                Content = $"This Employment Contract is made between {spec.Name} and {firstEmployeeName}.",
                Status = "Final",
                CreatedAt = DateTime.UtcNow.AddDays(-20),
                UpdatedAt = DateTime.UtcNow.AddDays(-20),
                FinalizedAt = DateTime.UtcNow.AddDays(-20)
            }, cancellationToken);

            await _unitOfWork.AuditLogs.AddAsync(new AuditLog
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                UserId = owner.Id,
                Action = "DEMO_DATA_SEEDED",
                Details = $"Demo data reset for {spec.Name}.",
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return logins;
    }

    private static User NewUser(Guid companyId, string fullName, string email, UserRole role, string passwordHash, ref int phoneCounter)
    {
        phoneCounter++;
        return new User
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Email = email,
            PasswordHash = passwordHash,
            FullName = fullName,
            Phone = $"+2010{phoneCounter}",
            Role = role,
            IsActive = true,
            IsEmailConfirmed = true,
            MustChangePassword = false,
            ActivationToken = string.Empty,
            ActivationTokenExpiry = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static string ToEmailLocalPart(string fullName) =>
        fullName.ToLowerInvariant().Replace(" ", ".");

    private sealed record DemoCompanySpec(
        string Name,
        string Slug,
        string TaxId,
        string Industry,
        string Address,
        string Phone,
        string OwnerName,
        string HrName,
        DemoEmployeeSpec[] Employees);

    private sealed record DemoEmployeeSpec(string FullName, string Department, string JobTitle);
}
