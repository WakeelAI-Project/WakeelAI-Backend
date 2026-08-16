using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Threading;

namespace Wakeel.API.Middleware;

internal class RateLimitEntry
{
    public int Count;
    public DateTime ExpiresAt;
}

/// <summary>
/// Simple in-memory per-identity rate limiter with per-endpoint limits.
/// Keying: user_id claim if present, otherwise remote IP address.
/// Routes and limits (per minute):
/// - /api/ai/chat (POST) -> 20 req/min/user
/// - /api/documents/generate (POST) -> 10 req/min/user
/// - everything else -> 100 req/min/user
/// Responses: 429 with Retry-After header containing seconds to reset.
/// </summary>
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IMemoryCache _cache;
    private readonly ILogger<RateLimitingMiddleware> _logger;

    public RateLimitingMiddleware(RequestDelegate next, IMemoryCache cache, ILogger<RateLimitingMiddleware> logger)
    {
        _next = next;
        _cache = cache;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            var path = context.Request.Path.Value ?? string.Empty;
            var method = context.Request.Method ?? "";

            // Determine route category
            var keyRoute = GetRouteKey(path, method);
            var limit = GetLimitForRoute(keyRoute);
            var window = TimeSpan.FromMinutes(1);

            // Determine identity
            string identity = GetIdentity(context) ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";

            var cacheKey = $"ratelimit:{keyRoute}:{identity}";

            var now = DateTime.UtcNow;

            var entry = _cache.GetOrCreate(cacheKey, e =>
            {
                var re = new RateLimitEntry { Count = 0, ExpiresAt = now.Add(window) };
                e.AbsoluteExpirationRelativeToNow = window;
                return re;
            });

            var current = Interlocked.Increment(ref entry.Count);

            if (current > limit)
            {
                var retryAfter = (int)Math.Ceiling((entry.ExpiresAt - now).TotalSeconds);
                if (retryAfter < 0) retryAfter = 0;

                _logger.LogWarning("Rate limit exceeded for {Identity} route={Route} limit={Limit}", identity, keyRoute, limit);

                context.Response.Headers["Retry-After"] = retryAfter.ToString();
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.Response.ContentType = "application/json";
                var payload = new { error = "rate_limited", message = "Too many requests.", status = 429 };
                await context.Response.WriteAsJsonAsync(payload);
                return;
            }

            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RateLimitingMiddleware failed");
            throw;
        }
    }

    private static string GetIdentity(HttpContext context)
    {
        // prefer JWT company/user id claim "user_id" or ClaimTypes.NameIdentifier
        var user = context.User;
        if (user?.Identity != null && user.Identity.IsAuthenticated)
        {
            var userId = user.FindFirst("user_id")?.Value ?? user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId)) return userId;
        }

        // Fallback to header X-User-Id used by internal services
        if (context.Request.Headers.TryGetValue("X-User-Id", out var headerUserId))
            return headerUserId.ToString();

        return null;
    }

    private static string GetRouteKey(string path, string method)
    {
        // normalize
        var p = path.TrimEnd('/').ToLowerInvariant();
        if (p == "/api/ai/chat" && string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase))
            return "chat_ask";
        if (p == "/api/documents/generate" && string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase))
            return "documents_generate";
        return "default";
    }

    private static int GetLimitForRoute(string routeKey) => routeKey switch
    {
        "chat_ask" => 20,
        "documents_generate" => 10,
        _ => 100
    };
}
