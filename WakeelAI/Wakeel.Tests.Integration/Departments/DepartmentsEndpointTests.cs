using System;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using Wakeel.API;
using Wakeel.Application.DTOs.Departments;

namespace Wakeel.Tests.Integration.Departments;

public class DepartmentsEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public DepartmentsEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    // Helper method to simulate Login & Auth just like your .http file
    private async Task<HttpClient> GetAuthenticatedOwnerClientAsync()
    {
        var client = _factory.CreateClient();
        var email = $"owner_{Guid.NewGuid()}@test.com";
        var password = "Password123!";

        // 1. Register new company & owner
        await client.PostAsJsonAsync("/api/auth/register-company", new
        {
            company_name = "Department Test Corp",
            tax_id = "999888777",
            owner_full_name = "Admin",
            owner_email = email,
            password = password
        });

        // 2. Login to get access_token
        var loginRes = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        var json = await loginRes.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var token = doc.RootElement.GetProperty("access_token").GetString();

        // 3. Attach token to client headers
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task Department_CRUD_Lifecycle_ShouldSucceed()
    {
        // Arrange
        var client = await GetAuthenticatedOwnerClientAsync();

        // 1. CREATE -> Expect 201 Created
        var createReq = new CreateDepartmentRequest { Name = "IT Department", Description = "Tech stuff" };
        var createRes = await client.PostAsJsonAsync("/api/departments", createReq);
        createRes.StatusCode.Should().Be(HttpStatusCode.Created);

        var createdDept = await createRes.Content.ReadFromJsonAsync<DepartmentResponse>();
        createdDept.Should().NotBeNull();
        createdDept!.Name.Should().Be("IT Department");

        // 2. GET SINGLE -> Expect 200 OK
        var getRes = await client.GetAsync($"/api/departments/{createdDept.Id}");
        getRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // 3. UPDATE -> Expect 200 OK
        var updateReq = new UpdateDepartmentRequest { Name = "Engineering", Description = "Updated tech stuff" };
        var updateRes = await client.PatchAsJsonAsync($"/api/departments/{createdDept.Id}", updateReq);
        updateRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var updatedDept = await updateRes.Content.ReadFromJsonAsync<DepartmentResponse>();
        updatedDept!.Name.Should().Be("Engineering");

        // 4. DELETE -> Expect 204 No Content
        var deleteRes = await client.DeleteAsync($"/api/departments/{createdDept.Id}");
        deleteRes.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 5. GET SINGLE (Verify Deletion) -> Expect 404 Not Found
        var getAfterDeleteRes = await client.GetAsync($"/api/departments/{createdDept.Id}");
        getAfterDeleteRes.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateDepartment_WithoutToken_ShouldReturn401Unauthorized()
    {
        // Arrange: Client WITHOUT token
        var client = _factory.CreateClient();
        var createReq = new CreateDepartmentRequest { Name = "HR" };

        // Act
        var response = await client.PostAsJsonAsync("/api/departments", createReq);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateDepartment_WithInvalidData_ShouldReturn400BadRequest()
    {
        // Arrange
        var client = await GetAuthenticatedOwnerClientAsync();
        var createReq = new CreateDepartmentRequest { Name = "" }; // Invalid: Name is required

        // Act
        var response = await client.PostAsJsonAsync("/api/departments", createReq);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}