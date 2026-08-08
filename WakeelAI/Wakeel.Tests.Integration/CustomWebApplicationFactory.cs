using Wakeel.API;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wakeel.Infrastructure.Persistence;

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
        $"Server=(local);Database={_testDatabaseName};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True;";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((context, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string?>("ConnectionStrings:DefaultConnection", TestConnectionString)
            });
        });

        builder.ConfigureServices(services =>
        {
            using var scope = services.BuildServiceProvider().CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Fresh local database per test run: drop if it somehow already exists,
            // then apply all real migrations so the schema matches production exactly.
            dbContext.Database.EnsureDeleted();
            dbContext.Database.Migrate();
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