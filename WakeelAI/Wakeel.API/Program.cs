using Scalar.AspNetCore;
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

        // Register Scalar/OpenAPI helpers
        builder.Services.AddOpenApi();

        // CORS - allow all for integration during development
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowAll", policy => policy
                .AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader());
        });

        // Application & Infrastructure
        builder.Services.AddApplicationServices();
        builder.Services.AddInfrastructureServices(builder.Configuration);

        var app = builder.Build();


        app.MapOpenApi();
        app.MapScalarApiReference();

        app.UseHttpsRedirection();

        // Enable CORS
        app.UseCors("AllowAll");

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();

        app.Run();
    }
}

public partial class Program { }
