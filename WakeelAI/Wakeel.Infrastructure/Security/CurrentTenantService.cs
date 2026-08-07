using System;
using Wakeel.Application.Interfaces;

namespace Wakeel.Infrastructure.Security;

/// <summary>
/// Default implementation of ICurrentTenantService.
/// Holds the current tenant's company ID in memory for the duration of the HTTP request scope.
/// </summary>
public class CurrentTenantService : ICurrentTenantService
{
    /// <summary>
    /// The company ID of the current tenant, or null if not yet set.
    /// </summary>
    public Guid? CompanyId { get; private set; }

    /// <summary>
    /// True if CompanyId has been set, false otherwise.
    /// </summary>
    public bool HasTenant => CompanyId.HasValue;

    /// <summary>
    /// Sets the tenant's company ID for this request.
    /// </summary>
    /// <param name="companyId">The company ID to set.</param>
    public void SetTenant(Guid companyId)
    {
        CompanyId = companyId;
    }
}