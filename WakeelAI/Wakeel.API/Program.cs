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

        // TODO: This permissive CORS policy is for development/testing only. 
        // It must be restricted to specific frontend origins before production.
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowAll", policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            });
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

        var app = builder.Build();

        // OpenAPI & Scalar
        // if (app.Environment.IsDevelopment())
        // {
        //     app.MapOذpenApi();
        //     app.MapScalarApiReference();
        // }

        // Swagger
        app.UseSwagger();
        app.UseSwaggerUI();

        app.UseHttpsRedirection();

        // Ensure wwwroot exists so StaticFileMiddleware can serve files, even if created lazily later
        var webRoot = app.Environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        if (!Directory.Exists(webRoot))
        {
            Directory.CreateDirectory(webRoot);
        }
        
        app.UseStaticFiles();

        app.UseCors("AllowAll");

        app.UseAuthentication();
        // Internal M2M PSK middleware — secures all /api/ai/ routes.
        // Must run AFTER UseAuthentication so the middleware pipeline order is correct,
        // but the InternalApiKeyMiddleware itself bypasses JWT for /api/ai/ routes entirely.
        app.UseMiddleware<InternalApiKeyMiddleware>();
        app.UseMiddleware<TenantResolutionMiddleware>();
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
}

public partial class Program { }