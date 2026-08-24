using Wakeel.API;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wakeel.Infrastructure.Persistence;
using Wakeel.Application.Interfaces;
using Wakeel.Infrastructure.Security;
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
    /// <summary>
    /// appsettings.json intentionally ships with an empty Jwt:SecretKey (FIX-S1: secrets are
    /// never committed). The JWT bearer handler reads that key EAGERLY while services are being
    /// registered - i.e. before <c>ConfigureAppConfiguration</c> deltas from this factory are
    /// merged in - so the test signing key has to be visible to the very first configuration
    /// build. An environment variable is the only provider available that early.
    /// This is a throwaway test key, not a secret.
    /// </summary>
    static CustomWebApplicationFactory()
    {
        Environment.SetEnvironmentVariable(
            "Jwt__SecretKey",
            "test-signing-key-for-integration-tests-only-32+chars");

        // FIX-S3: Program.cs reads Cors:AllowedOrigins synchronously while registering
        // services, before this factory's ConfigureAppConfiguration delta is merged in
        // (same timing issue as Jwt:SecretKey above) - an environment variable is the
        // only provider available that early.
        Environment.SetEnvironmentVariable(
            "Cors__AllowedOrigins__0",
            "https://allowed.integrationtest.local");
    }

    /// <summary>Throwaway 32-byte AES key for tests only - not a secret, never used outside this test host.</summary>
    private static readonly string TestEncryptionKey = Convert.ToBase64String(new byte[32]);

    private readonly string _testDatabaseName = $"WakeelTestDb_{Guid.NewGuid():N}";
    private string TestConnectionString
    {
        get
        {
            var envConnection = Environment.GetEnvironmentVariable("TEST_CONNECTION_STRING")
                ?? Environment.GetEnvironmentVariable("ConnectionStrings__TestConnection");

            if (!string.IsNullOrWhiteSpace(envConnection))
            {
                return envConnection;
            }

            return $"Server=(localdb)\\mssqllocaldb;Database={_testDatabaseName};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True;";
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((context, configBuilder) =>
        {

            configBuilder.AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string?>("ConnectionStrings:DefaultConnection", TestConnectionString),
                // Required by InternalApiKeyMiddleware and AiNodeClient HttpClient at startup
                new KeyValuePair<string, string?>("AiNode:InternalApiKey", "test-internal-key"),
                new KeyValuePair<string, string?>("AiNode:BaseUrl", "http://localhost:3001"),
                // appsettings.json intentionally ships with an empty Jwt:SecretKey (secrets are
                // never committed), so the test host must supply its own signing key. Must be at
                // least 32 characters for HMAC-SHA256.
                new KeyValuePair<string, string?>("Jwt:SecretKey", "test-signing-key-for-integration-tests-only-32+chars"),
                // FIX-26: EmployeeProfile's Salary/NationalId converters need a valid key at
                // model-build time - appsettings.json ships with an empty placeholder.
                new KeyValuePair<string, string?>("Encryption:Key", TestEncryptionKey)
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

            var encryptionConfig = new ConfigurationBuilder()
                .AddInMemoryCollection(new[] { new KeyValuePair<string, string?>("Encryption:Key", TestEncryptionKey) })
                .Build();

            using var dbContext = new ApplicationDbContext(
                optionsBuilder.Options,
                new DesignTimeCurrentTenantService(),
                new FieldEncryptionService(encryptionConfig)
            );
            dbContext.Database.EnsureDeleted();
        }

        base.Dispose(disposing);
    }
}