using Wakeel.API;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wakeel.Infrastructure.Persistence;
using Wakeel.Application.Interfaces;
using Wakeel.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Wakeel.Tests.Integration;

/// <summary>
/// Pins the test host to the Development environment, then overrides the SQL Server
/// connection string to point at a fresh, randomly-named LOCAL database created and
/// dropped per test run — so integration tests never touch the real dev database
/// (which points at the remote hosted server) and run fast, offline, and isolated.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IDisposable
{
    private readonly string _testDatabaseName = $"WakeelTestDb_{Guid.NewGuid():N}";
    private string TestConnectionString =>
        $"Server=(localdb)\\mssqllocaldb;Database={_testDatabaseName};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True;";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((context, configBuilder) =>
        {

            configBuilder.AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string?>("ConnectionStrings:DefaultConnection", TestConnectionString),
                // Required by InternalApiKeyMiddleware and AiNodeClient HttpClient at startup
                new KeyValuePair<string, string?>("AiNode:InternalApiKey", "test-internal-key"),
                new KeyValuePair<string, string?>("AiNode:BaseUrl", "http://localhost:3001")
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddScoped<IEmailSender, FileEmailSender>();

            using var scope = services.BuildServiceProvider().CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Fresh local database per test run: drop if it somehow already exists,
            // then create the schema from the current model. Use EnsureCreated here
            // to avoid EF Core complaining about pending model changes caused by
            // test-time modifications (seed data GUIDs or newly added entities).
            dbContext.Database.EnsureDeleted();
            dbContext.Database.EnsureCreated();
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            optionsBuilder.UseSqlServer(TestConnectionString);

            using var dbContext = new ApplicationDbContext(
                optionsBuilder.Options,
                new DesignTimeCurrentTenantService()
            );
            dbContext.Database.EnsureDeleted();
        }

        base.Dispose(disposing);
    }
}