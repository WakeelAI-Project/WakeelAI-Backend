using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Wakeel.Application.DTOs.Documents;

namespace Wakeel.Application.Interfaces;

public interface IDocumentService
{
    Task<(IEnumerable<DocumentSummary> Data, int Total)> GetDocumentsAsync(
        int page, int limit, string? type, string? status, Guid? employeeId, string? sort, string? order);

    Task<DocumentDetail> GetDocumentByIdAsync(Guid documentId);

    Task UpdateDocumentAsync(Guid documentId, UpdateDocumentRequest request);

    Task FinalizeDocumentAsync(Guid documentId);

    Task SendEmailAsync(Guid documentId, SendEmailRequest request);
}
