using System;
using Wakeel.Application.Interfaces;

namespace Wakeel.Infrastructure.Persistence;

/// <summary>
/// No-op implementation of ICurrentTenantService used at design time
/// (e.g., during `dotnet ef migrations add`) when no HTTP request context exists.
///
/// This is provided to ApplicationDbContextFactory so that EF Core can create
/// the DbContext without requiring an actual ICurrentTenantService bean.
/// All methods are no-ops; HasTenant always returns false.
/// </summary>
public class DesignTimeCurrentTenantService : ICurrentTenantService
{
    /// <summary>
    /// Always returns null at design time (no tenant context).
    /// </summary>
    public Guid? CompanyId => null;

    /// <summary>
    /// Always returns false at design time.
    /// </summary>
    public bool HasTenant => false;

    /// <summary>
    /// No-op. Does nothing at design time.
    /// </summary>
    /// <param name="companyId">Ignored.</param>
    public void SetTenant(Guid companyId) { }
}