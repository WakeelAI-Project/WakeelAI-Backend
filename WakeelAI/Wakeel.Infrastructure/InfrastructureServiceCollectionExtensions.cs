using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Wakeel.Application.Interfaces;
using Wakeel.Application.Interfaces.Repositories;
using Wakeel.Infrastructure.Persistence;
using Wakeel.Infrastructure.Repositories;
using Wakeel.Infrastructure.Services;
using Wakeel.Infrastructure.Security;

namespace Wakeel.Infrastructure;

/// <summary>
/// Registers Infrastructure-layer services (database context, repositories, unit of work,
/// security services, and JWT authentication) with the dependency injection container.
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
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IRefreshTokenHasher, Sha256RefreshTokenHasher>();
        services.AddScoped<ICurrentTenantService, CurrentTenantService>();


        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<IDepartmentRepository, DepartmentRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();


        // Use file-based email sender for Development if SMTP is not configured
        var smtpHost = configuration["Smtp:Host"];
        if (string.IsNullOrWhiteSpace(smtpHost))
            services.AddScoped<IEmailSender, FileEmailSender>();
        else
            services.AddScoped<IEmailSender, SmtpEmailSender>();

        services.AddScoped<IFileService, LocalFileService>();

        // Register the named HttpClient for Node.js AI service calls.
        // 60-second timeout is required to prevent orphaned LLM generation tasks.
        services.AddHttpClient("AiNodeClient", client =>
        {
            client.BaseAddress = new Uri(
                configuration["AiNode:BaseUrl"]
                ?? throw new InvalidOperationException("AiNode:BaseUrl is not configured."));
            client.Timeout = TimeSpan.FromSeconds(60);
        });

        services.AddJwtAuthentication(configuration);

        return services;
    }

    private static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var secretKey = configuration["Jwt:SecretKey"]
            ?? throw new InvalidOperationException("Jwt:SecretKey is not configured.");
        var issuer = configuration["Jwt:Issuer"]
            ?? throw new InvalidOperationException("Jwt:Issuer is not configured.");
        var audience = configuration["Jwt:Audience"]
            ?? throw new InvalidOperationException("Jwt:Audience is not configured.");

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = issuer,
                    ValidAudience = audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
            });

        services.AddAuthorization();

        return services;
    }
}