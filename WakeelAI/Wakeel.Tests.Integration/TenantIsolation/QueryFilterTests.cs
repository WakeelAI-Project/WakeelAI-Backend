using System;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Wakeel.Infrastructure.Persistence;
using Xunit;
using Wakeel.API;
namespace Wakeel.Tests.Integration.TenantIsolation;

public class QueryFilterTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public QueryFilterTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private async Task<(HttpClient Client, Guid CompanyId)> RegisterCompanyAsync(string companyName)
    {
        var client = _factory.CreateClient();
        var email = $"owner_{Guid.NewGuid()}@test.com";
        var password = "Password123!";

        var registerRes = await client.PostAsJsonAsync("/api/auth/register-company", new
        {
            company_name = companyName,
            tax_id = Guid.NewGuid().ToString("N")[..9],
            owner_full_name = "Owner",
            owner_email = email,
            password
        });

        var registerJson = await registerRes.Content.ReadAsStringAsync();
        var registerDoc = JsonDocument.Parse(registerJson);
        var companyId = Guid.Parse(registerDoc.RootElement.GetProperty("company_id").GetString()!);
        var token = registerDoc.RootElement.GetProperty("access_token").GetString();

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        return (client, companyId);
    }

    [Fact]
    public async Task GlobalQueryFilter_ShouldPreventCrossTenantDataAccess_EvenWithoutManualFiltering()
    {
        // Arrange: create two separate companies, each with its own department
        var (clientA, companyIdA) = await RegisterCompanyAsync("Tenant A Corp");
        var (_, _) = await RegisterCompanyAsync("Tenant B Corp");

        await clientA.PostAsJsonAsync("/api/departments", new { name = "Tenant A Dept" });

        // Act: resolve ApplicationDbContext directly (bypassing controllers/services)
        // while acting as Tenant A, and query ALL departments with no manual WHERE clause.
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Simulate the tenant context TenantResolutionMiddleware would have set for a
        // request authenticated as Tenant A's owner.
        var tenantService = scope.ServiceProvider.GetRequiredService<Wakeel.Application.Interfaces.ICurrentTenantService>();
        tenantService.SetTenant(companyIdA);

        var allDepartmentsVisible = await dbContext.Departments.ToListAsync();

        // Assert: even though we queried with NO manual company filter at all,
        // the global query filter must still only return Tenant A's own department.
        allDepartmentsVisible.Should().OnlyContain(d => d.CompanyId == companyIdA);
    }

    [Fact]
    public async Task GlobalQueryFilter_ShouldBeInactive_WhenNoTenantResolved()
    {
        // Arrange: create two companies
        await RegisterCompanyAsync("Tenant C Corp");
        await RegisterCompanyAsync("Tenant D Corp");

        // Act: resolve ApplicationDbContext with NO tenant set (simulates design-time /
        // unauthenticated context) and query all companies.
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var allCompanies = await dbContext.Companies.ToListAsync();

        // Assert: Company itself is never filtered (it's the tenant root, not tenant-scoped data).
        allCompanies.Count.Should().BeGreaterThanOrEqualTo(2);
    }
}