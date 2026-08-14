using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wakeel.Application.DTOs.Templates;
using Wakeel.Application.Interfaces;

namespace Wakeel.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "HR_Manager")]
public class TemplatesController : ControllerBase
{
    private readonly ITemplateService _templateService;

    public TemplatesController(ITemplateService templateService)
    {
        _templateService = templateService ?? throw new ArgumentNullException(nameof(templateService));
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
}
