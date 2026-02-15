using System.ComponentModel.DataAnnotations;
using BuildingManagement.Core.Enums;

namespace BuildingManagement.Core.Entities;

public class TenantProfile : BaseEntity
{
    public int UnitId { get; set; }
    public Unit Unit { get; set; } = null!;

    /// <summary>Link to Identity user if tenant can login (nullable).</summary>
    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }

    [Required, MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(30)]
    public string? Phone { get; set; }

    [MaxLength(200)]
    public string? Email { get; set; }

    public DateTime? MoveInDate { get; set; }

    public DateTime? MoveOutDate { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsArchived { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    /// <summary>Owner / Landlord / Renter</summary>
    public PropertyRole PropertyRole { get; set; } = PropertyRole.Renter;

    /// <summary>Whether the tenant is also a house committee member.</summary>
    public bool IsCommitteeMember { get; set; }

    /// <summary>Marketing consent – happy to stay in touch.</summary>
    public bool MarketingConsent { get; set; }

    /// <summary>When the tenant accepted the terms of use.</summary>
    public DateTime? TermsAcceptedAtUtc { get; set; }
}
