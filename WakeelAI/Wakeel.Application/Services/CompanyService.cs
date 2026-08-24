using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wakeel.Application.DTOs.Company;
using Wakeel.Application.Interfaces;
using Wakeel.Application.Interfaces.Repositories;

namespace Wakeel.Application.Services;

public class CompanyService : ICompanyService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<CompanyService> _logger;

    public CompanyService(IUnitOfWork unitOfWork, IAuditLogService auditLogService, ILogger<CompanyService> logger)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _auditLogService = auditLogService ?? throw new ArgumentNullException(nameof(auditLogService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

    public async Task<CompanyProfileDto> GetCompanyProfileAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var company = await _unitOfWork.Companies.GetByIdAsync(companyId, cancellationToken);
        if (company == null)
        {
            throw new InvalidOperationException("company_not_found");
        }

        return new CompanyProfileDto
        {
            Id = company.Id,
            Name = company.Name,
            TaxId = company.TaxId,
            Industry = company.Industry,
            Address = company.Address,
            PhoneNumber = company.PhoneNumber,
            Email = company.Email,
            LogoUrl = company.LogoUrl,
            WorkingHours = company.WorkingHours,
            RegisteredAt = company.RegisteredAt
        };
    }

    public async Task<CompanyProfileDto> UpdateCompanyProfileAsync(Guid companyId, Guid actorUserId, UpdateCompanyProfileDto request, string? logoUrl = null, CancellationToken cancellationToken = default)
    {
        var company = await _unitOfWork.Companies.GetByIdAsync(companyId, cancellationToken);
        if (company == null)
            throw new InvalidOperationException("company_not_found");

        if (request.IsAddressProvided)
            company.Address = request.Address ?? string.Empty;
        
        if (request.IsPhoneNumberProvided)
            company.PhoneNumber = request.PhoneNumber ?? string.Empty;
        
        if (request.IsEmailProvided)
            company.Email = request.Email ?? string.Empty;
        
        if (request.IsIndustryProvided)
            company.Industry = request.Industry ?? string.Empty;
        
        if (request.IsWorkingHoursProvided)
            company.WorkingHours = request.WorkingHours ?? string.Empty;

        if (logoUrl != null)
        {
            company.LogoUrl = logoUrl;
        }

        _unitOfWork.Companies.Update(company);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await TryLogAuditAsync(actorUserId, "COMPANY_PROFILE_UPDATED", $"Updated company profile for \"{company.Name}\".");

        return new CompanyProfileDto
        {
            Id = company.Id,
            Name = company.Name,
            TaxId = company.TaxId,
            Industry = company.Industry,
            Address = company.Address,
            PhoneNumber = company.PhoneNumber,
            Email = company.Email,
            LogoUrl = company.LogoUrl,
            WorkingHours = company.WorkingHours,
            RegisteredAt = company.RegisteredAt
        };
    }
}
