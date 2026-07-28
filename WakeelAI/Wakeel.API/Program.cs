using Scalar.AspNetCore;
using Wakeel.Application;
using Wakeel.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Wakeel.Infrastructure.Persistence;

namespace Wakeel.API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

            builder.Services.AddControllers();
            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            builder.Services.AddOpenApi();

            // Register application services (business logic, validators, etc.)
            builder.Services.AddApplicationServices();

            // Register infrastructure services (password hasher, repositories, database, etc.)
            builder.Services.AddInfrastructureServices();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
                app.MapScalarApiReference();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}
