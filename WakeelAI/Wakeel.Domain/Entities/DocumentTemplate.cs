using System;

namespace Wakeel.Domain.Entities;

/// <summary>
/// Represents a document template used by the AI to generate documents.
/// Managed strictly by HR.
/// </summary>
public class DocumentTemplate
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    
    public string DocumentType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ContentTemplate { get; set; } = string.Empty;
    
    public bool IsActive { get; set; }
    
    // Navigation property
    public Company Company { get; set; } = null!;
}
