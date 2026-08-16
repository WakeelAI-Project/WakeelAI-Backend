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
using Wakeel.Application.DTOs.Templates;
using Wakeel.Application.Interfaces;

namespace Wakeel.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "HR_Manager")]
public class TemplatesController : ControllerBase
{
    private readonly ITemplateService _templateService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TemplatesController> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public TemplatesController(
        ITemplateService templateService,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<TemplatesController> logger)
    {
        _templateService   = templateService ?? throw new ArgumentNullException(nameof(templateService));
        _httpClientFactory = httpClientFactory;
        _configuration     = configuration;
        _logger            = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetTemplates(
        [FromQuery] string? documentType,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20)
    {
        var (templates, total) = await _templateService.GetTemplatesAsync(page, limit, documentType);

        return Ok(new
        {
            data = templates,
            page = page,
            limit = limit,
            total = total
        });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetTemplate(Guid id)
    {
        try
        {
            var template = await _templateService.GetTemplateByIdAsync(id);
            return Ok(template);
        }
        catch (InvalidOperationException ex) when (ex.Message == "template_not_found")
        {
            return NotFound(new { error = new { code = "template_not_found", message = "Template not found." } });
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateTemplate([FromBody] CreateTemplateRequest request)
    {
        var template = await _templateService.CreateTemplateAsync(request);
        return CreatedAtAction(nameof(GetTemplate), new { id = template.Id }, template);
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> UpdateTemplate(Guid id, [FromBody] UpdateTemplateRequest request)
    {
        try
        {
            var template = await _templateService.UpdateTemplateAsync(id, request);
            return Ok(template);
        }
        catch (InvalidOperationException ex) when (ex.Message == "template_not_found")
        {
            return NotFound(new { error = new { code = "template_not_found", message = "Template not found." } });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTemplate(Guid id)
    {
        try
        {
            await _templateService.DeleteTemplateAsync(id);
            return NoContent();
        }
        catch (InvalidOperationException ex) when (ex.Message == "template_not_found")
        {
            return NotFound(new { error = new { code = "template_not_found", message = "Template not found." } });
        }
    }

    [HttpPost("{template_id:guid}/generate-clauses")]
    [ProducesResponseType(typeof(NodeTemplateClausesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GenerateClauses(
        [FromRoute] Guid template_id,
        [FromBody] GenerateClausesRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(new ApiErrorResponse { Error = "validation_error", Message = "Invalid payload.", Status = 400 });

        var language = (request.Language ?? "en").Trim().ToLowerInvariant();
        if (language != "en" && language != "ar")
            return BadRequest(new ApiErrorResponse { Error = "validation_error", Message = "language must be 'en' or 'ar'.", Status = 400 });

        if (!request.IncludeLaborLaw && !request.IncludeCompanyPolicy)
            return BadRequest(new ApiErrorResponse { Error = "validation_error", Message = "At least one of include_labor_law or include_company_policy must be true.", Status = 400 });

        // Identity comes ONLY from the JWT — never from the request body.
        var companyIdClaim = User.FindFirstValue("company_id");
        var userIdClaim    = User.FindFirstValue("user_id");
        var role           = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role");
        if (!Guid.TryParse(companyIdClaim, out var companyId) || !Guid.TryParse(userIdClaim, out var userId) || string.IsNullOrWhiteSpace(role))
            return Forbid();

        // Tenant-safe template resolution: GetTemplateByIdAsync goes through EF,
        // and the DocumentTemplate global query filter scopes by the JWT tenant,
        // so another company's template id yields template_not_found -> 404.
        TemplateDto template;
        try
        {
            template = await _templateService.GetTemplateByIdAsync(template_id);
        }
        catch (InvalidOperationException ex) when (ex.Message == "template_not_found")
        {
            return NotFound(new { error = new { code = "template_not_found", message = "Template not found." } });
        }

        var nodePayload = new
        {
            templateId           = template.Id.ToString(),
            documentType         = template.DocumentType,
            templateName         = template.Name,
            companyId            = companyId.ToString(),   // trusted: from JWT
            language             = language,
            includeLaborLaw      = request.IncludeLaborLaw,
            includeCompanyPolicy = request.IncludeCompanyPolicy,
            instruction          = string.IsNullOrWhiteSpace(request.Instruction) ? null : request.Instruction.Trim()
        };

        var internalApiKey = _configuration["AiNode:InternalApiKey"]
            ?? throw new InvalidOperationException("AiNode:InternalApiKey is not configured.");

        var client = _httpClientFactory.CreateClient("AiNodeClient");
        using var nodeRequest = new HttpRequestMessage(HttpMethod.Post, "/api/ai/template-clauses")
        {
            Content = JsonContent.Create(nodePayload)
        };
        nodeRequest.Headers.Add("X-Internal-API-Key", internalApiKey);
        nodeRequest.Headers.Add("X-User-Id",    userId.ToString());
        nodeRequest.Headers.Add("X-Company-Id", companyId.ToString());
        nodeRequest.Headers.Add("X-Role",       role);

        HttpResponseMessage nodeResponse;
        try
        {
            nodeResponse = await client.SendAsync(nodeRequest, cancellationToken);
        }
        catch (TaskCanceledException)
        {
            _logger.LogError("GenerateClauses: AI service timed out for TemplateId={TemplateId}.", template_id);
            return StatusCode(StatusCodes.Status504GatewayTimeout,
                new ApiErrorResponse { Error = "ai_timeout", Message = "The AI service did not respond in time. Please try again.", Status = 504 });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "GenerateClauses: Failed to reach AI service.");
            return StatusCode(StatusCodes.Status502BadGateway,
                new ApiErrorResponse { Error = "ai_unavailable", Message = "AI service is currently unavailable.", Status = 502 });
        }

        if (!nodeResponse.IsSuccessStatusCode)
        {
            _logger.LogWarning("GenerateClauses: AI service returned {StatusCode} for TemplateId={TemplateId}.",
                nodeResponse.StatusCode, template_id);
            // Do not leak internal AI error bodies.
            return StatusCode((int)nodeResponse.StatusCode,
                new ApiErrorResponse { Error = "ai_error", Message = "The AI service returned an error.", Status = (int)nodeResponse.StatusCode });
        }

        NodeTemplateClausesResponse? aiReply;
        try
        {
            aiReply = await nodeResponse.Content.ReadFromJsonAsync<NodeTemplateClausesResponse>(_jsonOptions, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "GenerateClauses: Failed to deserialize AI response.");
            return StatusCode(StatusCodes.Status502BadGateway,
                new ApiErrorResponse { Error = "ai_response_error", Message = "Could not parse AI service response.", Status = 502 });
        }

        if (aiReply is null)
            return StatusCode(StatusCodes.Status502BadGateway,
                new ApiErrorResponse { Error = "ai_response_error", Message = "AI service returned an empty response.", Status = 502 });

        // Non-mutating by design: nothing is written to the database here.
        return Ok(aiReply);
    }
}
