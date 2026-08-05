using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Wakeel.API;

namespace Wakeel.Tests.Integration;

/// <summary>
/// Pins the test host to the Development environment so it always resolves the local
/// SQLEXPRESS dev connection string (appsettings.Development.json), never the
/// Production appsettings.json remote connection string.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        base.ConfigureWebHost(builder);
    }
}
