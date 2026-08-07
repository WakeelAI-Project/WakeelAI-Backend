using System;

namespace Wakeel.Application.Interfaces;

/// <summary>
/// Scoped service that holds the current tenant's company ID for this request.
/// Set by TenantResolutionMiddleware after JWT validation; used by EF Core global query filters.
/// </summary>
public interface ICurrentTenantService
{
    /// <summary>
    /// The company ID of the current tenant, or null if no tenant has been resolved yet.
    /// </summary>
    Guid? CompanyId { get; }

    /// <summary>
    /// True if a tenant has been resolved for this request, false otherwise.
    /// </summary>
    bool HasTenant { get; }

    /// <summary>
    /// Sets the tenant for this request (called by TenantResolutionMiddleware).
    /// </summary>
    /// <param name="companyId">The company ID to set as the current tenant.</param>
    void SetTenant(Guid companyId);
}