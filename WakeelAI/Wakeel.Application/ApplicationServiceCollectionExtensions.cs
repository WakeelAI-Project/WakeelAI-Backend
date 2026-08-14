using Microsoft.Extensions.DependencyInjection;
using Wakeel.Application.Interfaces;
using Wakeel.Application.Services;
using Wakeel.Application.Interfaces.Services;

namespace Wakeel.Application;

/// <summary>
/// Extension methods for registering application services in the dependency injection container.
/// Centralizes all Application layer service registrations in one place.
/// </summary>
public static class ApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Registers all application services (business logic, validation, etc.) to the DI container.
    /// </summary>
    /// <param name="services">The service collection to register services into.</param>
    /// <returns>The same service collection for method chaining.</returns>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Register authentication service
        services.AddScoped<IAuthService, AuthService>();

        // Register newly implemented user and employee services
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IEmployeeService, EmployeeService>();
        services.AddScoped<ICompanyService, CompanyService>();
        services.AddScoped<IDepartmentService, DepartmentService>();
        services.AddScoped<ILeaveRequestService, LeaveRequestService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<ITemplateService, TemplateService>();

        return services;
    }
}