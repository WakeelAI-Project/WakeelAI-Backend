using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wakeel.API.Middleware;

/// <summary>
/// Middleware that secures all routes under /api/ai/ using a Pre-Shared Key (PSK)
/// instead of JWT. It validates the X-Internal-API-Key header and then enforces
/// the presence and validity of the required M2M identity headers.
///
/// This middleware must be registered BEFORE UseAuthentication() in Program.cs
/// so that internal routes are short-circuited before the JWT pipeline runs.
/// </summary>
public class InternalApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _expectedApiKey;
    private readonly ILogger<InternalApiKeyMiddleware> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    /// <summary>
    /// Initializes a new instance of the InternalApiKeyMiddleware class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="logger">The logger instance.</param>
    /// <exception cref="InvalidOperationException">Thrown if AiNode:InternalApiKey is not configured.</exception>
    public InternalApiKeyMiddleware(
        RequestDelegate next,
        IConfiguration configuration,
        ILogger<InternalApiKeyMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger;
        _expectedApiKey = configuration["AiNode:InternalApiKey"]
            ?? throw new InvalidOperationException("AiNode:InternalApiKey is not configured.");
    }

    private bool IsIncomingM2MEndpoint(PathString path)
    {
        var p = path.Value ?? string.Empty;

        // Explicitly intercept only INCOMING endpoints from Node.js -> .NET
        if (p.StartsWith("/api/ai/employee-context", StringComparison.OrdinalIgnoreCase)) return true;
        if (p.StartsWith("/api/ai/company-context", StringComparison.OrdinalIgnoreCase)) return true;
        if (p.StartsWith("/api/ai/templates/active", StringComparison.OrdinalIgnoreCase)) return true;
        if (p.StartsWith("/api/documents/save", StringComparison.OrdinalIgnoreCase)) return true;
        if (p.StartsWith("/api/ai/leave-requests", StringComparison.OrdinalIgnoreCase)) return true;

        // Do NOT intercept /api/ai/chat or /api/knowledge/ingest
        // because those are either public frontend requests or outbound from .NET

        return false;
    }

    /// <summary>
    /// Invokes the middleware. Only explicit incoming M2M endpoints are intercepted.
    /// Validates PSK then required identity headers.
    /// </summary>
    /// <param name="context">The HTTP context for this request.</param>
    /// <returns>A task that completes when the middleware pipeline has finished.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        // Only guard specific incoming M2M routes
        if (!IsIncomingM2MEndpoint(context.Request.Path))
        {
            await _next(context);
            return;
        }

        // -------- Step 1: Validate PSK --------
        var providedApiKey = context.Request.Headers["X-Internal-API-Key"].ToString();

        if (string.IsNullOrEmpty(providedApiKey) || providedApiKey != _expectedApiKey)
        {
            _logger.LogWarning("Internal API request to {Path} rejected: invalid or missing X-Internal-API-Key.", context.Request.Path);
            await WriteErrorResponseAsync(context, StatusCodes.Status401Unauthorized, "unauthorized", "Missing or invalid X-Internal-API-Key.");
            return;
        }

        // -------- Step 2: Validate required identity headers --------
        var userId     = context.Request.Headers["X-User-Id"].ToString();
        var companyId  = context.Request.Headers["X-Company-Id"].ToString();
        var role       = context.Request.Headers["X-Role"].ToString();

        if (string.IsNullOrWhiteSpace(userId) ||
            string.IsNullOrWhiteSpace(companyId) ||
            string.IsNullOrWhiteSpace(role))
        {
            _logger.LogWarning("Internal API request to {Path} rejected: missing identity headers.", context.Request.Path);
            await WriteErrorResponseAsync(context, StatusCodes.Status400BadRequest, "missing_identity_headers",
                "X-User-Id, X-Company-Id, and X-Role headers are all required.");
            return;
        }

        // Step 3: No custom format validation for UUID/Role here.
        // We let invalid formats pass through to downstream controllers 
        // to handle natively per the v8 spec requirements.

        await _next(context);
    }

    private static async Task WriteErrorResponseAsync(HttpContext context, int statusCode, string error, string message)
    {
        context.Response.StatusCode  = statusCode;
        context.Response.ContentType = "application/json";

        var body = JsonSerializer.Serialize(
            new InternalErrorResponse { Error = error, Message = message, Status = statusCode },
            _jsonOptions);

        await context.Response.WriteAsync(body);
    }

    private sealed class InternalErrorResponse
    {
        [JsonPropertyName("error")]
        public string Error { get; init; } = string.Empty;

        [JsonPropertyName("message")]
        public string Message { get; init; } = string.Empty;

        [JsonPropertyName("status")]
        public int Status { get; init; }
    }
}
