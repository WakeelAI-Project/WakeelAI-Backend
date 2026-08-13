using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Moq.Protected;
using Xunit;

namespace Wakeel.Tests.Integration.AiChat;

public class AiChatGatewayControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AiChatGatewayControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AskChat_ShouldForwardFieldValuesAndLanguageToNodeJs()
    {
        // 1. Arrange
        var nodeResponseJson = @"{
            ""message"": ""Hello"",
            ""action"": null,
            ""sources"": [],
            ""missing_fields"": [],
            ""is_complete"": true,
            ""conversation_id"": ""00000000-0000-0000-0000-000000000000""
        }";
        var nodeResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(nodeResponseJson)
        };

        HttpRequestMessage? capturedNodeRequest = null;
        string? capturedNodeRequestBody = null;

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .Callback<HttpRequestMessage, CancellationToken>(async (req, ct) =>
            {
                capturedNodeRequest = req;
                if (req.Content != null)
                {
                    capturedNodeRequestBody = await req.Content.ReadAsStringAsync();
                }
            })
            .ReturnsAsync(nodeResponse);

        // Replace the HttpClientFactory to return our mock
        var customFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddHttpClient("AiNodeClient")
                    .ConfigurePrimaryHttpMessageHandler(() => handlerMock.Object);
            });
        });
        var client = customFactory.CreateClient();

        // Register a test user and login to get JWT token
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var ownerEmail = $"owner_{suffix}@test.local";
        var registerResponse = await client.PostAsJsonAsync("/api/auth/register-company", new
        {
            company_name    = $"Test Company {suffix}",
            tax_id          = suffix,
            owner_full_name = "Test Owner",
            owner_email     = ownerEmail,
            password        = "TestPassword123!"
        });
        registerResponse.EnsureSuccessStatusCode();

        var empEmail = $"emp_{suffix}@test.local";
        using (var scope = customFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Wakeel.Infrastructure.Persistence.ApplicationDbContext>();
            var owner = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(db.Users, u => u.Email == ownerEmail);
            
            if (owner != null)
            {
                db.Users.Add(new Wakeel.Domain.Entities.User
                {
                    Id = Guid.NewGuid(),
                    CompanyId = owner.CompanyId,
                    FullName = "Test Employee",
                    Email = empEmail,
                    IsActive = true,
                    Role = Wakeel.Domain.Enums.UserRole.Employee,
                    PasswordHash = owner.PasswordHash
                });
                await db.SaveChangesAsync();
            }
        }

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = empEmail,
            password = "TestPassword123!"
        });
        var loginResultStr = await loginResponse.Content.ReadAsStringAsync();
        var loginResult = JsonDocument.Parse(loginResultStr).RootElement;
        var token = loginResult.TryGetProperty("token", out var t1) ? t1.GetString() : 
                    loginResult.TryGetProperty("access_token", out var t2) ? t2.GetString() : throw new Exception("Token not found: " + loginResultStr);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // 2. Act
        var askPayload = new
        {
            message = "Generate a certificate",
            language = "ar",
            field_values = new { employee_name = "Ali", reason = "Promotion" }
        };

        var response = await client.PostAsJsonAsync("/api/ai/chat", askPayload);
        var content = await response.Content.ReadAsStringAsync();
        
        // 3. Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, content);
        
        capturedNodeRequestBody.Should().NotBeNull();
        var nodeReqDoc = JsonDocument.Parse(capturedNodeRequestBody!);
        
        nodeReqDoc.RootElement.TryGetProperty("language", out var langProp).Should().BeTrue();
        langProp.GetString().Should().Be("ar");

        nodeReqDoc.RootElement.TryGetProperty("field_values", out var fieldsProp).Should().BeTrue();
        fieldsProp.GetProperty("employee_name").GetString().Should().Be("Ali");
        fieldsProp.GetProperty("reason").GetString().Should().Be("Promotion");
    }
}
