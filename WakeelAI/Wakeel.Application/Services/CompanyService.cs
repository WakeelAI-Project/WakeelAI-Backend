using System;
using System.Threading;
using System.Threading.Tasks;
using Wakeel.Application.DTOs.Company;
using Wakeel.Application.Interfaces;
using Wakeel.Application.Interfaces.Repositories;

namespace Wakeel.Application.Services;

public class CompanyService : ICompanyService
{
    private readonly IUnitOfWork _unitOfWork;

    public CompanyService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
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

    public async Task<CompanyProfileDto> UpdateCompanyProfileAsync(Guid companyId, UpdateCompanyProfileDto request, string? logoUrl = null, CancellationToken cancellationToken = default)
    {
        var company = await _unitOfWork.Companies.GetByIdAsync(companyId, cancellationToken);
        if (company == null)
            throw new InvalidOperationException("company_not_found");

        company.Address = request.Address ?? string.Empty;
        company.PhoneNumber = request.PhoneNumber ?? string.Empty;
        company.Email = request.Email ?? string.Empty;
        company.Industry = request.Industry ?? string.Empty;
        company.WorkingHours = request.WorkingHours ?? string.Empty;

        if (logoUrl != null)
        {
            company.LogoUrl = logoUrl;
        }

        _unitOfWork.Companies.Update(company);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

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
