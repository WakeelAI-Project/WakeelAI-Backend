using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wakeel.Application.Interfaces;
using Wakeel.Application.Interfaces.Repositories;
using Wakeel.Infrastructure.Persistence;
using Wakeel.Infrastructure.Repositories;
using Wakeel.Infrastructure.Security;

namespace Wakeel.Infrastructure;

/// <summary>
/// Registers Infrastructure-layer services (database context, repositories, unit of work,
/// and external service implementations) with the dependency injection container.
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
        //services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>(); // to be uncommented

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}