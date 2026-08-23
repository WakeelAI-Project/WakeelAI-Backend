using System;
using System.Threading;
using System.Threading.Tasks;
using Wakeel.Application.DTOs.Company;

namespace Wakeel.Application.Interfaces;

public interface ICompanyService
{
    Task<CompanyProfileDto> GetCompanyProfileAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<CompanyProfileDto> UpdateCompanyProfileAsync(Guid companyId, Guid actorUserId, UpdateCompanyProfileDto request, string? logoUrl = null, CancellationToken cancellationToken = default);
}
