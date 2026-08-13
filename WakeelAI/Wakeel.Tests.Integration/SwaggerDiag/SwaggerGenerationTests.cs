using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.Swagger;
using Xunit;
using Xunit.Abstractions;

namespace Wakeel.Tests.Integration.SwaggerDiag;

/// <summary>
/// Diagnostic test that programmatically generates the Swagger document
/// using the real application host to capture the exact exception.
/// </summary>
public class SwaggerGenerationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly ITestOutputHelper _output;

    public SwaggerGenerationTests(CustomWebApplicationFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    [Fact]
    public void SwaggerDocument_CanBeGenerated_WithoutExceptions()
    {
        // This triggers the full WebApplicationFactory host build
        using var scope = _factory.Services.CreateScope();
        var swaggerProvider = scope.ServiceProvider.GetRequiredService<ISwaggerProvider>();

        Exception? caughtException = null;
        try
        {
            var doc = swaggerProvider.GetSwagger("v1");
            _output.WriteLine($"SUCCESS: Swagger document generated. Paths: {doc.Paths.Count}");
            foreach (var path in doc.Paths)
            {
                _output.WriteLine($"  Path: {path.Key}");
                foreach (var op in path.Value.Operations)
                {
                    _output.WriteLine($"    {op.Key}: {op.Value.OperationId ?? "(no operationId)"}");
                }
            }
        }
        catch (Exception ex)
        {
            caughtException = ex;
            _output.WriteLine("=== SWAGGER GENERATION FAILED ===");
            _output.WriteLine($"Exception Type: {ex.GetType().FullName}");
            _output.WriteLine($"Message: {ex.Message}");
            _output.WriteLine($"StackTrace:\n{ex.StackTrace}");
            
            var inner = ex.InnerException;
            while (inner != null)
            {
                _output.WriteLine($"\n--- Inner Exception ---");
                _output.WriteLine($"Type: {inner.GetType().FullName}");
                _output.WriteLine($"Message: {inner.Message}");
                _output.WriteLine($"StackTrace:\n{inner.StackTrace}");
                inner = inner.InnerException;
            }
        }

        caughtException.Should().BeNull(
            because: $"Swagger generation must succeed. Got: {caughtException?.GetType().Name}: {caughtException?.Message}");
    }
}
