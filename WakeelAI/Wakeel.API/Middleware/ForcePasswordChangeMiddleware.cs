using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Wakeel.Application.Interfaces.Repositories;
using Microsoft.Extensions.Hosting;

namespace Wakeel.API.Middleware;

public class ForcePasswordChangeMiddleware
{
    private static readonly string[] AllowedPathPrefixes =
    {
        "/api/account/change-password",
        "/api/auth/logout",
        "/api/auth/refresh",
        "/api/auth/login"
    };

    private readonly RequestDelegate _next;
    private readonly IHostEnvironment _environment;

    public ForcePasswordChangeMiddleware(RequestDelegate next, IHostEnvironment environment)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    public async Task InvokeAsync(HttpContext context, IUnitOfWork unitOfWork)
    {
        var isAuthenticated = context.User?.Identity?.IsAuthenticated == true;
        // Bypassed ONLY in the "Testing" environment, unless a test explicitly opts in
        // via X-Test-ForcePassword to exercise the enforcement path end-to-end.
        if (_environment.IsEnvironment("Testing") &&
            !context.Request.Headers.ContainsKey("X-Test-ForcePassword"))
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path.Value ?? string.Empty;

        var isAllowedPath = false;
        foreach (var prefix in AllowedPathPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                isAllowedPath = true;
                break;
            }
        }

        if (isAuthenticated && !isAllowedPath && path.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
        {
            var userIdClaim = context.User!.FindFirstValue("user_id")
                ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (Guid.TryParse(userIdClaim, out var userId))
            {
                // PERF: add a JWT claim in the future to avoid doing a DB read per request for authenticated users
                var user = await unitOfWork.Users.GetByIdAsync(userId);
                if (user is { MustChangePassword: true })
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsJsonAsync(new
                    {
                        error = "password_change_required",
                        message = "You must change your temporary password before using the application.",
                        status = 403
                    });
                    return;
                }
            }
        }

        await _next(context);
    }
}
