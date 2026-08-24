using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.OpenApi.Models;
using Wakeel.API.Middleware;

// using Scalar.AspNetCore;
using Wakeel.Application;
using Wakeel.Domain.Entities;
using Wakeel.Infrastructure;

namespace Wakeel.API;

public partial class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddHealthChecks();

        // Only the origins configured under Cors:AllowedOrigins may call this API from a
        // browser. In Development, an empty list falls back to the local Vite dev server so
        // the web app keeps working out of the box without committing a real origin.
        var configuredOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (configuredOrigins.Length == 0 && builder.Environment.IsDevelopment())
        {
            configuredOrigins = ["http://localhost:5173"];
        }

        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowConfiguredOrigins", policy =>
            {
                policy.WithOrigins(configuredOrigins)
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials();
            });
        });

        // FIX-23: the host sits behind a reverse proxy (IIS/ANCM on MonsterASP.NET),
        // so without this every request's RemoteIpAddress is the proxy's own loopback
        // address - RateLimitingMiddleware's per-IP fallback (used for anonymous routes
        // like login/forgot-password) then collapses every real client into one shared
        // bucket. KnownNetworks/KnownProxies are cleared because the proxy in front of
        // this single-instance deployment is the in-process ASP.NET Core Module itself,
        // not a fixed, externally-addressable IP that could be safely allow-listed here.
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });

        // Swagger
        builder.Services.AddSwaggerGen(c =>
        {
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Enter your JWT access token"
            });

            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    new List<string>()
                }
            });
        });

        // OpenAPI
        // builder.Services.AddOpenApi();

        // Application & Infrastructure
        builder.Services.AddApplicationServices();
        builder.Services.AddInfrastructureServices(builder.Configuration);

        builder.Services.AddHostedService<Wakeel.API.BackgroundServices.DailyMaintenanceBackgroundService>();

        var app = builder.Build();

        // Fail fast when a required secret is absent. Secrets are never committed:
        // appsettings.json ships with empty placeholders and real values come from
        // User Secrets (local development) or environment variables (deployment).
        // See appsettings.Example.json for the full list of keys.
        // Runs against the fully-built configuration (so every provider, including ones
        // contributed by a test host, has been applied) and before a single request is
        // served. Never logs the values themselves - only the names of the missing keys.
        EnsureRequiredConfiguration(app.Configuration);

        // FIX-26 data step: `dotnet run -- --backfill-employee-encryption` (or the published
        // exe with the same flag) encrypts every EMPLOYEE_PROFILE row's plaintext
        // NationalId/Salary into the *Enc columns, then exits without starting the web
        // server. Deliberately a manual, explicit command rather than something that runs
        // automatically at every startup - see
        // Wakeel.Infrastructure.Services.IEmployeeProfileEncryptionBackfillService for why.
        if (args.Contains("--backfill-employee-encryption"))
        {
            using var backfillScope = app.Services.CreateScope();
            var backfillService = backfillScope.ServiceProvider
                .GetRequiredService<Wakeel.Infrastructure.Services.IEmployeeProfileEncryptionBackfillService>();
            var backfilledCount = backfillService.BackfillAsync().GetAwaiter().GetResult();
            Console.WriteLine($"Employee profile encryption backfill complete: {backfilledCount} row(s) encrypted.");
            return;
        }

        // OpenAPI & Scalar
        // if (app.Environment.IsDevelopment())
        // {
        //     app.MapOذpenApi();
        //     app.MapScalarApiReference();
        // }

        // Must run before anything that reads the caller's IP/scheme - including
        // UseHttpsRedirection below and RateLimitingMiddleware further down - so both
        // see the real client, not the reverse proxy's.
        app.UseForwardedHeaders();

        // Swagger
        app.UseSwagger();
        app.UseSwaggerUI();

        app.UseHttpsRedirection();

        // Global error handling middleware must be early in the pipeline so it can
        // translate exceptions into the standardized API v2 error envelope.
        app.UseMiddleware<GlobalErrorHandlingMiddleware>();

        // Rate limiting will be applied after authentication so the middleware can
        // use the authenticated user's claims to key per-user limits.

        // Ensure wwwroot exists so StaticFileMiddleware can serve files, even if created lazily later
        var webRoot = app.Environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        if (!Directory.Exists(webRoot))
        {
            Directory.CreateDirectory(webRoot);
        }
        
        app.UseStaticFiles();

        app.UseCors("AllowConfiguredOrigins");

        app.UseAuthentication();
        // Internal M2M PSK middleware — secures all /api/ai/ routes.
        // Must run AFTER UseAuthentication so the middleware pipeline order is correct,
        // but the InternalApiKeyMiddleware itself bypasses JWT for /api/ai/ routes entirely.
        app.UseMiddleware<InternalApiKeyMiddleware>();
        app.UseMiddleware<TenantResolutionMiddleware>();
        // Rate limiting - enforce per-user limits for chat/document generation and global default.
        // Requires IMemoryCache to be registered by AddInfrastructureServices (or AddMemoryCache elsewhere).
        app.UseMiddleware<RateLimitingMiddleware>();
        app.UseMiddleware<ForcePasswordChangeMiddleware>();
        app.UseAuthorization();


        app.MapControllers();

        // Root endpoint
        app.MapGet("/", () => Results.Ok(new
        {
            message = "Welcome to Wakeel AI API",
            version = "v1",
            swagger = "/swagger",
            health = "/health"
        }));

        // Health Check endpoint
        app.MapHealthChecks("/health");

        app.Run();
    }

    /// <summary>
    /// Validates that every configuration key the application cannot run without has been
    /// supplied. Throws an <see cref="InvalidOperationException"/> naming the missing key(s)
    /// so a misconfigured deployment fails at startup instead of at first request.
    /// Values are never written to logs or exception messages.
    /// </summary>
    /// <param name="configuration">The application configuration to validate.</param>
    /// <exception cref="InvalidOperationException">Thrown when a required key is missing, empty, or whitespace.</exception>
    internal static void EnsureRequiredConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        string[] requiredKeys =
        [
            "ConnectionStrings:DefaultConnection",
            "Jwt:SecretKey",
            "AiNode:InternalApiKey",
            "Encryption:Key"
        ];

        var missing = requiredKeys
            .Where(key => string.IsNullOrWhiteSpace(configuration[key]))
            .ToList();

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"Required configuration is missing: {string.Join(", ", missing)}. " +
                "Supply these via User Secrets (local development) or environment variables " +
                "(deployment, e.g. Jwt__SecretKey). See appsettings.Example.json.");
        }

        // FIX-26: Encryption:Key must decode to exactly 32 bytes (AES-256), not merely be
        // present - a malformed or wrong-length key would otherwise fail lazily, the first
        // time an EmployeeProfile row is read or written, rather than at startup.
        try
        {
            Wakeel.Infrastructure.Security.FieldEncryptionService.ParseKey(configuration["Encryption:Key"]);
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException($"Encryption:Key is invalid: {ex.Message}", ex);
        }
    }
}

public partial class Program { }