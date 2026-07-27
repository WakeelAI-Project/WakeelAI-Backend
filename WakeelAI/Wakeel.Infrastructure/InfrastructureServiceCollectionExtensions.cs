using Microsoft.Extensions.DependencyInjection;
using Wakeel.Application.Interfaces;
using Wakeel.Infrastructure.Security;

namespace Wakeel.Infrastructure;

/// <summary>
/// Extension methods for registering infrastructure services in the dependency injection container.
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registers all infrastructure services (security, persistence, external services) to the DI container.
    /// </summary>
    /// <param name="services">The service collection to register services into.</param>
    /// <returns>The same service collection for method chaining.</returns>
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        // Register password hasher implementation
        services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();

        // TODO: Register database context here when available
        // services.AddScoped<IUnitOfWork, UnitOfWork>();
        // services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

        return services;
    }
}
