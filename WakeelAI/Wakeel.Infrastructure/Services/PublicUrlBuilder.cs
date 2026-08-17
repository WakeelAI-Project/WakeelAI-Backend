using System;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Wakeel.Application.Interfaces;

namespace Wakeel.Infrastructure.Services;

public class PublicUrlBuilder : IPublicUrlBuilder
{
    private readonly IConfiguration _configuration;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PublicUrlBuilder(IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
    {
        _configuration = configuration;
        _httpContextAccessor = httpContextAccessor;
    }

    public string ToAbsoluteUrl(string? relativeOrAbsoluteUrl)
    {
        if (string.IsNullOrWhiteSpace(relativeOrAbsoluteUrl))
            return string.Empty;

        // Already absolute — return as-is.
        if (Uri.TryCreate(relativeOrAbsoluteUrl, UriKind.Absolute, out var absolute)
            && (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps))
            return relativeOrAbsoluteUrl;

        var baseUrl = ResolveBaseUrl();
        if (string.IsNullOrWhiteSpace(baseUrl))
            return relativeOrAbsoluteUrl; // last-resort: unchanged

        return $"{baseUrl.TrimEnd('/')}/{relativeOrAbsoluteUrl.TrimStart('/')}";
    }

    private string? ResolveBaseUrl()
    {
        // 1) Explicit configuration wins (needed behind reverse proxies / for background jobs).
        var configured = _configuration["App:PublicBaseUrl"];
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;

        // 2) Fall back to the current HTTP request's scheme + host.
        var request = _httpContextAccessor.HttpContext?.Request;
        if (request != null)
            return $"{request.Scheme}://{request.Host}";

        return null;
    }
}
