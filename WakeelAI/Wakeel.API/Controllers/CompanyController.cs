using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wakeel.Application.DTOs.Company;
using Wakeel.Application.DTOs.Users; // for ApiErrorResponse
using Wakeel.Application.Interfaces;
using System.IO;
using System.Linq;

namespace Wakeel.API.Controllers;

[ApiController]
[Route("api/company")]
public class CompanyController : ControllerBase
{
    private readonly ICompanyService _companyService;
    private readonly IFileService _fileService;

    public CompanyController(ICompanyService companyService, IFileService fileService)
    {
        _companyService = companyService;
        _fileService = fileService;
    }

    [Authorize(Roles = "Company_Owner")]
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
                // Validate file size (max 5MB)
                const long maxFileSize = 5 * 1024 * 1024;
                if (logo.Length > maxFileSize)
                    return BadRequest(new ApiErrorResponse { Error = "file_too_large", Message = "Logo file exceeds the 5MB limit.", Status = 400 });

                // Validate file extension/type
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                var extension = Path.GetExtension(logo.FileName).ToLowerInvariant();
                if (string.IsNullOrEmpty(extension) || !allowedExtensions.Contains(extension))
                    return BadRequest(new ApiErrorResponse { Error = "invalid_file_type", Message = "Only image files (.jpg, .jpeg, .png, .webp) are allowed.", Status = 400 });

                using var stream = logo.OpenReadStream();
                logoUrl = await _fileService.SaveFileAsync(stream, logo.FileName, "company_logos", cancellationToken);
            }

            var formKeys = Request.Form.Keys.Select(k => k.ToLowerInvariant()).ToHashSet();

            var dtoToUpdate = new UpdateCompanyProfileDto
            {
                Address = request.Address,
                IsAddressProvided = formKeys.Contains("address"),

                PhoneNumber = request.PhoneNumber,
                IsPhoneNumberProvided = formKeys.Contains("phone_number") || formKeys.Contains("phonenumber"),

                Email = request.Email,
                IsEmailProvided = formKeys.Contains("email"),

                Industry = request.Industry,
                IsIndustryProvided = formKeys.Contains("industry"),

                WorkingHours = request.WorkingHours,
                IsWorkingHoursProvided = formKeys.Contains("working_hours") || formKeys.Contains("workinghours")
            };

            var updatedProfile = await _companyService.UpdateCompanyProfileAsync(companyId, dtoToUpdate, logoUrl, cancellationToken);
            return Ok(updatedProfile);
        }
        catch (InvalidOperationException ex) when (ex.Message == "company_not_found")
        {
            return NotFound(new ApiErrorResponse { Error = "company_not_found", Message = "Company not found.", Status = 404 });
        }
    }
}
