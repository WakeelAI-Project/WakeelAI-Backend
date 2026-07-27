using Microsoft.Extensions.DependencyInjection;
using Wakeel.Application.Interfaces;

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
        // TODO: Register IAuthService implementation when AuthService is implemented
        // Uncomment the line below once AuthService.cs is created in the Application layer
        // services.AddScoped<IAuthService, AuthService>();

        return services;
    }
}