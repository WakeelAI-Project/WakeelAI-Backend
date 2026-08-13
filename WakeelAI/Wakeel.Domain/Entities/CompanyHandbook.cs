using System;

namespace Wakeel.Domain.Entities;

/// <summary>
/// Represents a company policy handbook document uploaded by a Company Owner.
/// Once uploaded, the document's text content is ingested into the Node.js RAG pipeline
/// for use in AI-assisted policy queries.
/// </summary>
public class CompanyHandbook
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public Guid UploadedByUserId { get; set; }
    public DateTime UploadedAt { get; set; }

    public Company Company { get; set; } = null!;
    public User UploadedByUser { get; set; } = null!;
}
