using System;
using System.Collections.Generic;

namespace Wakeel.Domain.Entities;

/// <summary>
/// Represents a company entity in the system.
/// Contains basic company information and relationships to users and employee profiles.
/// </summary>
public class Company
{
    /// <summary>
    /// Unique identifier for the company.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The official name of the company.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The tax identification number of the company (e.g., VAT number, Business License ID).
    /// </summary>
    public string TaxId { get; set; } = string.Empty;

    /// <summary>
    /// The industry or sector the company operates in (e.g., Technology, Manufacturing, Services).
    /// </summary>
    public string Industry { get; set; } = string.Empty;

    /// <summary>
    /// The physical address of the company's headquarters or main office.
    /// </summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>
    /// The date and time when the company was registered in the system.
    /// </summary>
    public DateTime RegisteredAt { get; set; }

    /// <summary>
    /// Indicates whether the company is currently active in the system.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Contact phone number for the company.
    /// </summary>
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>
    /// Contact email for the company.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// The relative URL or path to the company's uploaded logo.
    /// </summary>
    public string LogoUrl { get; set; } = string.Empty;

    /// <summary>
    /// Text description of the company's working hours.
    /// </summary>
    public string WorkingHours { get; set; } = string.Empty;

    /// <summary>
    /// Navigation property: Collection of users belonging to this company.
    /// </summary>
    public ICollection<User> Users { get; set; } = new List<User>();
}
