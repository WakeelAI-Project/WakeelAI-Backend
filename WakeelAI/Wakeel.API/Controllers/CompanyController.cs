using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;
using Wakeel.Application.DTOs.Company;
using Wakeel.Application.DTOs.Users;
using Wakeel.Application.Interfaces;
using Wakeel.Domain.Entities;
using Wakeel.Infrastructure.Persistence;

namespace Wakeel.API.Controllers;

[ApiController]
[Route("api/company")]
public class CompanyController : ControllerBase
{
    private readonly ICompanyService _companyService;
    private readonly IFileService _fileService;
    private readonly ApplicationDbContext _dbContext;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CompanyController> _logger;

    private static readonly string[] AllowedLogoExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
    private const long MaxLogoSizeBytes = 5 * 1024 * 1024;   // 5 MB
    private const long MaxPdfSizeBytes  = 20 * 1024 * 1024;  // 20 MB

    public CompanyController(
        ICompanyService companyService,
        IFileService fileService,
        ApplicationDbContext dbContext,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<CompanyController> logger)
    {
        _companyService    = companyService;
        _fileService       = fileService;
        _dbContext         = dbContext;
        _httpClientFactory = httpClientFactory;
        _configuration     = configuration;
        _logger            = logger;
    }

    [Authorize(Roles = "Company_Owner,HR_Manager")]
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
    {
        var companyIdClaim = User.FindFirst("company_id")?.Value;
        if (!Guid.TryParse(companyIdClaim, out var companyId))
            return Forbid();

        try
        {
            var profile = await _companyService.GetCompanyProfileAsync(companyId, cancellationToken);
            return Ok(profile);
        }
        catch (InvalidOperationException ex) when (ex.Message == "company_not_found")
        {
            return NotFound(new ApiErrorResponse { Error = "company_not_found", Message = "Company not found.", Status = 404 });
        }
    }

    [Authorize(Roles = "Company_Owner")]
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromForm] UpdateCompanyProfileDto request, IFormFile? logo, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(new ApiErrorResponse { Error = "validation_error", Message = "Invalid payload.", Status = 400 });

        var companyIdClaim = User.FindFirst("company_id")?.Value;
        if (!Guid.TryParse(companyIdClaim, out var companyId))
            return Forbid();

        try
        {
            string? logoUrl = null;
            if (logo != null && logo.Length > 0)
            {
                if (logo.Length > MaxLogoSizeBytes)
                    return BadRequest(new ApiErrorResponse { Error = "file_too_large", Message = "Logo file exceeds the 5MB limit.", Status = 400 });

                var extension = Path.GetExtension(logo.FileName).ToLowerInvariant();
                if (string.IsNullOrEmpty(extension) || !AllowedLogoExtensions.Contains(extension))
                    return BadRequest(new ApiErrorResponse { Error = "invalid_file_type", Message = "Only image files (.jpg, .jpeg, .png, .webp) are allowed.", Status = 400 });

                using var stream = logo.OpenReadStream();
                logoUrl = await _fileService.SaveFileAsync(stream, logo.FileName, "company_logos", cancellationToken);
            }

            var formKeys = Request.Form.Keys.Select(k => k.ToLowerInvariant()).ToHashSet();

            var dtoToUpdate = new UpdateCompanyProfileDto
            {
                Address              = request.Address,
                IsAddressProvided    = formKeys.Contains("address"),
                PhoneNumber          = request.PhoneNumber,
                IsPhoneNumberProvided = formKeys.Contains("phone_number") || formKeys.Contains("phonenumber"),
                Email                = request.Email,
                IsEmailProvided      = formKeys.Contains("email"),
                Industry             = request.Industry,
                IsIndustryProvided   = formKeys.Contains("industry"),
                WorkingHours         = request.WorkingHours,
                IsWorkingHoursProvided = formKeys.Contains("working_hours") || formKeys.Contains("workinghours")
            };

            var updatedProfile = await _companyService.UpdateCompanyProfileAsync(companyId, dtoToUpdate, logoUrl, cancellationToken);
            return Ok(updatedProfile);
        }
        catch (InvalidOperationException ex) when (ex.Message == "company_not_found")
        {
            return NotFound(new ApiErrorResponse { Error = "company_not_found", Message = "Company not found.", Status = 400 });
        }
    }

    /// <summary>
    /// Uploads a company policy handbook PDF. The file is saved locally, a CompanyHandbook
    /// record is created in SQL Server, and the extracted text is forwarded to the Node.js
    /// RAG ingestion pipeline. The Node.js call is best-effort — failure is logged but does
    /// not prevent a 201 response (the handbook is already persisted).
    /// </summary>
    /// <param name="pdf">The policy PDF file (max 20 MB).</param>
    /// <param name="title">A human-readable title for the handbook.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation.</param>
    /// <returns>201 Created with handbook_id, title, file_url, and uploaded_at.</returns>
    [Authorize(Roles = "Company_Owner")]
    [HttpPost("policies")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadPolicy(
        IFormFile? pdf,
        [FromForm] string? title,
        CancellationToken cancellationToken)
    {
        // -------- Validate inputs --------
        if (pdf == null || pdf.Length == 0)
            return BadRequest(new ApiErrorResponse { Error = "validation_error", Message = "A PDF file is required.", Status = 400 });

        if (string.IsNullOrWhiteSpace(title))
            return BadRequest(new ApiErrorResponse { Error = "validation_error", Message = "title is required.", Status = 400 });

        var ext = Path.GetExtension(pdf.FileName).ToLowerInvariant();
        if (ext != ".pdf")
            return BadRequest(new ApiErrorResponse { Error = "invalid_file_type", Message = "Only PDF files are accepted.", Status = 400 });

        if (pdf.Length > MaxPdfSizeBytes)
            return BadRequest(new ApiErrorResponse { Error = "file_too_large", Message = "PDF exceeds the 20 MB size limit.", Status = 400 });

        // -------- Extract identity from JWT --------
        var companyIdClaim = User.FindFirst("company_id")?.Value;
        var userIdClaim    = User.FindFirst("user_id")?.Value;
        var role           = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role") ?? "Company_Owner";

        if (!Guid.TryParse(companyIdClaim, out var companyId) || !Guid.TryParse(userIdClaim, out var userId))
            return Forbid();

        // -------- Save file --------
        string fileUrl;
        using (var fileStream = pdf.OpenReadStream())
        {
            fileUrl = await _fileService.SaveFileAsync(fileStream, pdf.FileName, "company_handbooks", cancellationToken);
        }

        // -------- Extract PDF text with PdfPig --------
        string extractedText;
        using (var pdfStream = pdf.OpenReadStream())
        {
            var textBuilder = new StringBuilder();
            using var doc = PdfDocument.Open(pdfStream);
            foreach (var page in doc.GetPages())
            {
                textBuilder.AppendLine(page.Text);
            }
            extractedText = textBuilder.ToString().Trim();
        }

        // -------- Persist handbook record --------
        var handbookId = Guid.NewGuid();
        var handbook = new CompanyHandbook
        {
            Id                = handbookId,
            CompanyId         = companyId,
            Title             = title.Trim(),
            FileUrl           = fileUrl,
            UploadedByUserId  = userId,
            UploadedAt        = DateTime.UtcNow
        };

        _dbContext.CompanyHandbooks.Add(handbook);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "CompanyHandbook saved: Id={HandbookId}, CompanyId={CompanyId}, Title={Title}",
            handbookId, companyId, title);

        // -------- Forward to Node.js RAG ingestion (best-effort) --------
        await ForwardToRagIngestionAsync(handbookId, companyId, userId, role, title.Trim(), extractedText);

        return Created(string.Empty, new
        {
            handbook_id = handbook.Id,
            title       = handbook.Title,
            file_url    = handbook.FileUrl,
            uploaded_at = handbook.UploadedAt
        });
    }

    /// <summary>
    /// Sends the extracted PDF text to the Node.js RAG knowledge ingestion endpoint.
    /// Uses the exact v8 payload contract. Failure is non-fatal — exceptions are caught and logged.
    /// </summary>
    private async Task ForwardToRagIngestionAsync(
        Guid handbookId,
        Guid companyId,
        Guid userId,
        string role,
        string title,
        string content)
    {
        try
        {
            var internalApiKey = _configuration["AiNode:InternalApiKey"]
                ?? throw new InvalidOperationException("AiNode:InternalApiKey is not configured.");

            // Exact v8 payload contract
            var ingestionPayload = new
            {
                companyId     = companyId.ToString(),
                knowledgeType = "company-policy",
                documentId    = handbookId.ToString(),
                title         = title,
                content       = content
            };

            var client = _httpClientFactory.CreateClient("AiNodeClient");

            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/knowledge/ingest")
            {
                Content = JsonContent.Create(ingestionPayload)
            };

            // Attach all 4 required M2M headers
            request.Headers.Add("X-Internal-API-Key", internalApiKey);
            request.Headers.Add("X-User-Id",    userId.ToString());
            request.Headers.Add("X-Company-Id", companyId.ToString());
            request.Headers.Add("X-Role",        role);

            var response = await client.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "RAG ingestion returned non-success for HandbookId={HandbookId}: {StatusCode}",
                    handbookId, response.StatusCode);
            }
            else
            {
                _logger.LogInformation("RAG ingestion succeeded for HandbookId={HandbookId}.", handbookId);
            }
        }
        catch (Exception ex)
        {
            // Graceful degradation: handbook is already persisted; log the failure and continue.
            _logger.LogWarning(ex,
                "RAG ingestion failed for HandbookId={HandbookId}. The handbook is saved but not yet indexed.",
                handbookId);
        }
    }
}
