using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Wakeel.Application.DTOs.Chat;

namespace Wakeel.API.Controllers;

/// <summary>
/// API Gateway controller for AI chat. Acts as a proxy between the client and the Node.js AI service.
/// .NET owns the conversationId lifecycle — it mints new UUIDs and translates
/// the Node.js response envelope into the client-facing snake_case contract.
/// </summary>
[ApiController]
[Route("api/chat")]
[Authorize]
public class AiChatGatewayController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AiChatGatewayController> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Initializes a new instance of the AiChatGatewayController.
    /// </summary>
    public AiChatGatewayController(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<AiChatGatewayController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration     = configuration;
        _logger            = logger;
    }

    // -------- Identity helpers --------

    private Guid GetUserId()    => Guid.Parse(User.FindFirstValue("user_id")    ?? throw new UnauthorizedAccessException());
    private Guid GetCompanyId() => Guid.Parse(User.FindFirstValue("company_id") ?? throw new UnauthorizedAccessException());
    private string GetRole()    => User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role") ?? throw new UnauthorizedAccessException();

    // -------- POST /api/chat/ask --------

    /// <summary>
    /// Accepts a chat message from the client, resolves or mints a conversationId,
    /// and proxies the request to the Node.js AI service.
    /// Returns the AI reply translated into the client-facing envelope.
    /// </summary>
    /// <param name="request">The client's chat request.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation.</param>
    /// <returns>200 OK with the AI reply envelope.</returns>
    [HttpPost("ask")]
    [Authorize(Roles = "HR_Manager,Employee")]
    [ProducesResponseType(typeof(AskChatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> Ask(
        [FromBody] AskChatRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(new ApiErrorResponse { Error = "validation_error", Message = "message is required.", Status = 400 });

        var userId    = GetUserId();
        var companyId = GetCompanyId();
        var role      = GetRole();

        // -------- conversationId lifecycle --------
        // .NET is the owner. Generate a new UUID if the client did not provide one.
        var conversationId = string.IsNullOrWhiteSpace(request.ConversationId)
            ? Guid.NewGuid().ToString()
            : request.ConversationId;

        _logger.LogInformation(
            "Chat ask: UserId={UserId}, CompanyId={CompanyId}, ConversationId={ConversationId}",
            userId, companyId, conversationId);

        // -------- Build Node.js payload --------
        var nodePayload = new
        {
            message        = request.Message,
            conversationId = conversationId,
            language       = request.Language,
            fieldValues    = request.FieldValues
        };

        var internalApiKey = _configuration["AiNode:InternalApiKey"]
            ?? throw new InvalidOperationException("AiNode:InternalApiKey is not configured.");

        var client = _httpClientFactory.CreateClient("AiNodeClient");

        using var nodeRequest = new HttpRequestMessage(HttpMethod.Post, "/api/ai/chat")
        {
            Content = JsonContent.Create(nodePayload)
        };

        // -------- Attach M2M identity headers --------
        nodeRequest.Headers.Add("X-Internal-API-Key", internalApiKey);
        nodeRequest.Headers.Add("X-User-Id",    userId.ToString());
        nodeRequest.Headers.Add("X-Company-Id", companyId.ToString());
        nodeRequest.Headers.Add("X-Role",        role);

        HttpResponseMessage nodeResponse;
        try
        {
            nodeResponse = await client.SendAsync(nodeRequest, cancellationToken);
        }
        catch (TaskCanceledException)
        {
            _logger.LogError("Chat ask: Node.js AI service timed out for ConversationId={ConversationId}.", conversationId);
            return StatusCode(StatusCodes.Status504GatewayTimeout,
                new ApiErrorResponse { Error = "ai_timeout", Message = "The AI service did not respond in time. Please try again.", Status = 504 });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Chat ask: Failed to reach Node.js AI service.");
            return StatusCode(StatusCodes.Status502BadGateway,
                new ApiErrorResponse { Error = "ai_unavailable", Message = "AI service is currently unavailable.", Status = 502 });
        }

        if (!nodeResponse.IsSuccessStatusCode)
        {
            _logger.LogWarning("Chat ask: Node.js AI service returned {StatusCode} for ConversationId={ConversationId}.",
                nodeResponse.StatusCode, conversationId);
            return StatusCode((int)nodeResponse.StatusCode,
                new ApiErrorResponse { Error = "ai_error", Message = "The AI service returned an error.", Status = (int)nodeResponse.StatusCode });
        }

        // -------- Translate Node.js response into client envelope --------
        NodeAiChatResponse? aiReply;
        try
        {
            aiReply = await nodeResponse.Content.ReadFromJsonAsync<NodeAiChatResponse>(_jsonOptions, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Chat ask: Failed to deserialize Node.js response.");
            return StatusCode(StatusCodes.Status502BadGateway,
                new ApiErrorResponse { Error = "ai_response_error", Message = "Could not parse AI service response.", Status = 502 });
        }

        if (aiReply is null)
            return StatusCode(StatusCodes.Status502BadGateway,
                new ApiErrorResponse { Error = "ai_response_error", Message = "AI service returned an empty response.", Status = 502 });

        var response = new AskChatResponse
        {
            ChatId         = Guid.NewGuid(),        // Generated by .NET
            ConversationId = conversationId,         // Owned by .NET
            Reply          = aiReply.Message,        // Mapped from Node.js "message" field
            Sources        = aiReply.Sources,
            MissingFields  = aiReply.MissingFields,
            ResultCard     = aiReply.ResultCard,
            CreatedAt      = DateTime.UtcNow        // Generated by .NET
        };

        return Ok(response);
    }

    // -------- GET /api/chat/history --------

    /// <summary>
    /// Proxies the chat history request to the Node.js AI service.
    /// conversation_id is required. Page and limit are forwarded as query parameters.
    /// </summary>
    /// <param name="conversationId">The ID of the conversation thread to retrieve history for.</param>
    /// <param name="page">Page number (1-indexed).</param>
    /// <param name="limit">Number of messages per page.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation.</param>
    /// <returns>200 OK with the history payload forwarded from Node.js.</returns>
    [HttpGet("history")]
    [Authorize(Roles = "HR_Manager,Employee")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetHistory(
        [FromQuery(Name = "conversation_id")] string? conversationId,
        [FromQuery] int page  = 1,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
            return BadRequest(new ApiErrorResponse
            {
                Error   = "validation_error",
                Message = "conversation_id query parameter is required.",
                Status  = 400
            });

        var userId    = GetUserId();
        var companyId = GetCompanyId();
        var role      = GetRole();

        var internalApiKey = _configuration["AiNode:InternalApiKey"]
            ?? throw new InvalidOperationException("AiNode:InternalApiKey is not configured.");

        var nodeUrl = $"/api/ai/chat/history?conversationId={Uri.EscapeDataString(conversationId)}&page={page}&limit={limit}";

        var client = _httpClientFactory.CreateClient("AiNodeClient");

        using var nodeRequest = new HttpRequestMessage(HttpMethod.Get, nodeUrl);
        nodeRequest.Headers.Add("X-Internal-API-Key", internalApiKey);
        nodeRequest.Headers.Add("X-User-Id",    userId.ToString());
        nodeRequest.Headers.Add("X-Company-Id", companyId.ToString());
        nodeRequest.Headers.Add("X-Role",        role);

        HttpResponseMessage nodeResponse;
        try
        {
            nodeResponse = await client.SendAsync(nodeRequest, cancellationToken);
        }
        catch (TaskCanceledException)
        {
            return StatusCode(StatusCodes.Status504GatewayTimeout,
                new ApiErrorResponse { Error = "ai_timeout", Message = "The AI service did not respond in time.", Status = 504 });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Chat history: Failed to reach Node.js AI service.");
            return StatusCode(StatusCodes.Status502BadGateway,
                new ApiErrorResponse { Error = "ai_unavailable", Message = "AI service is currently unavailable.", Status = 502 });
        }

        // Forward the raw response body from Node.js as-is
        var rawBody = await nodeResponse.Content.ReadAsStringAsync(cancellationToken);
        return new ContentResult
        {
            Content     = rawBody,
            ContentType = "application/json",
            StatusCode  = (int)nodeResponse.StatusCode
        };
    }
}
