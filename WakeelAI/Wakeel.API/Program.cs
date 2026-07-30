<<<<<<< HEAD
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Wakeel.Infrastructure.Persistence;
=======
using Microsoft.OpenApi;
using Scalar.AspNetCore;
using Wakeel.Application;
using Wakeel.Infrastructure;
>>>>>>> a1e16be97fe87f91487bdd174f6d7b6ddcca41f4

namespace Wakeel.API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
<<<<<<< HEAD
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

=======
>>>>>>> a1e16be97fe87f91487bdd174f6d7b6ddcca41f4
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();

            //// swagger
            //builder.Services.AddSwaggerGen(c =>
            //{
            //    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            //    {
            //        Name = "Authorization",
            //        Type = SecuritySchemeType.Http,
            //        Scheme = "Bearer",
            //        BearerFormat = "JWT",
            //        In = ParameterLocation.Header,
            //        Description = "Enter your access token"
            //    });
            //    c.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            //    {
            //        {
            //            new OpenApiSecuritySchemeReference("Bearer"),
            //            new List<string>()
            //        }
            //    });
            //});

            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            builder.Services.AddOpenApi();

            // Register application services (business logic, validators, etc.)
            builder.Services.AddApplicationServices();

            // Register infrastructure services (DbContext, Repositories, UnitOfWork, Security, etc.)
            builder.Services.AddInfrastructureServices(builder.Configuration);

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
                app.MapScalarApiReference();
                //app.UseSwagger();
                //app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}
public partial class Program { }
