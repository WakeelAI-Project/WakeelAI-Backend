using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Wakeel.Application.DTOs.Templates;

namespace Wakeel.Application.Interfaces;

public interface ITemplateService
{
    Task<(IEnumerable<TemplateDto> Data, int Total)> GetTemplatesAsync(
        int page, int limit, string? documentType);

    Task<TemplateDto> GetTemplateByIdAsync(Guid id);

    Task<TemplateDto> CreateTemplateAsync(CreateTemplateRequest request);

    Task<TemplateDto> UpdateTemplateAsync(Guid id, UpdateTemplateRequest request);

    Task DeleteTemplateAsync(Guid id);
}
