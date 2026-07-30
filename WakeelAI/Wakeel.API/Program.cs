using Microsoft.OpenApi.Models;
// using Scalar.AspNetCore;
using Wakeel.Application;
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

        app.UseAuthentication();
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