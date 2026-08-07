using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Wakeel.Application.Interfaces;

namespace Wakeel.API.Middleware;

/// <summary>
/// Middleware that resolves the current tenant from the "company_id" JWT claim
/// and stores it in the scoped ICurrentTenantService.
///
/// Reads ONLY from the JWT claim, never from request body, query string, or headers.
/// If no valid "company_id" claim is present (e.g., /auth/login, /auth/register-company),
/// no tenant is set and EF Core global query filters remain inactive (no-op).
///
/// This middleware must run AFTER UseAuthentication() to access User.Claims.
/// </summary>
public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>
    /// Initializes a new instance of the TenantResolutionMiddleware class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <exception cref="ArgumentNullException">Thrown if next is null.</exception>
    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
    }

    /// <summary>
    /// Invokes the middleware to resolve and set the current tenant.
    /// </summary>
    /// <param name="context">The HTTP context for this request.</param>
    /// <param name="currentTenantService">The scoped tenant service to set the company ID on.</param>
    /// <returns>A task that completes when the middleware pipeline has finished.</returns>
    public async Task InvokeAsync(HttpContext context, ICurrentTenantService currentTenantService)
    {
        var companyIdClaim = context.User?.FindFirst("company_id")?.Value;

        if (!string.IsNullOrEmpty(companyIdClaim) && Guid.TryParse(companyIdClaim, out var companyId))
        {
            currentTenantService.SetTenant(companyId);
        }

        await _next(context);
    }
}