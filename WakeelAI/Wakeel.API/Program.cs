using Microsoft.OpenApi;
using Scalar.AspNetCore;
using Wakeel.Application;
using Wakeel.Infrastructure;

namespace Wakeel.API;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();

        // Health Checks
        builder.Services.AddHealthChecks();

        // Swagger
        builder.Services.AddSwaggerGen(c =>
        {
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "Bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Enter your JWT access token"
            });

            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecuritySchemeReference("Bearer"),
                    Array.Empty<string>()
                }
            });
        });

        builder.Services.AddOpenApi();

        // Application & Infrastructure
        builder.Services.AddApplicationServices();
        builder.Services.AddInfrastructureServices(builder.Configuration);

        var app = builder.Build();

        // OpenAPI
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.MapScalarApiReference();
        }

        // Swagger (available in all environments)
        app.UseSwagger();
        app.UseSwaggerUI();

        app.UseHttpsRedirection();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();

        // Root endpoint
        app.MapGet("/", () =>
        {
            return Results.Ok(new
            {
                message = "Welcome to Wakeel AI API",
                version = "v1",
                swagger = "/swagger",
                health = "/health"
            });
        });

        // Health Check endpoint
        app.MapHealthChecks("/health");

        app.Run();
    }
}

public partial class Program { }