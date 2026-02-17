using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BuildingManagement.Core.Entities.Finance;

/// <summary>
/// Invoice issued by a building manager to the building committee for management services.
/// Backed by an external Israeli invoicing provider document.
/// </summary>
public class ManagerInvoice
{
    public int Id { get; set; }

    [Required]
    public string ManagerUserId { get; set; } = string.Empty;
    public ApplicationUser ManagerUser { get; set; } = null!;

    public int BuildingId { get; set; }
    public Building Building { get; set; } = null!;

    /// <summary>Billing period in YYYY-MM format.</summary>
    [Required, MaxLength(7)]
    public string Period { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [MaxLength(200)]
    public string? InvoiceDocId { get; set; }

    [MaxLength(100)]
    public string? InvoiceDocNumber { get; set; }

    [MaxLength(2000)]
    public string? InvoicePdfUrl { get; set; }

    public DateTime? IssuedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
