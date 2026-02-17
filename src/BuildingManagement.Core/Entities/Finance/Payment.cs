using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BuildingManagement.Core.Enums;

namespace BuildingManagement.Core.Entities.Finance;

public class Payment
{
    public int Id { get; set; }

    public int UnitId { get; set; }
    public Unit Unit { get; set; } = null!;

    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    public DateTime PaymentDateUtc { get; set; } = DateTime.UtcNow;

    public int? PaymentMethodId { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }

    [MaxLength(200)]
    public string? ProviderReference { get; set; }

    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    /// <summary>True when entered manually by manager (not via payment gateway).</summary>
    public bool IsManual { get; set; }

    /// <summary>Manual payment method type (BankTransfer, Cash, Check, Manual).</summary>
    public PaymentMethodType? ManualMethodType { get; set; }

    /// <summary>Free-text notes for manual payments.</summary>
    [MaxLength(1000)]
    public string? Notes { get; set; }

    /// <summary>UserId of manager who entered the manual payment.</summary>
    [MaxLength(450)]
    public string? EnteredByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // ─── Receipt (קבלה) fields ─────────────────────────────
    [MaxLength(200)]
    public string? ReceiptDocId { get; set; }

    [MaxLength(100)]
    public string? ReceiptDocNumber { get; set; }

    [MaxLength(2000)]
    public string? ReceiptPdfUrl { get; set; }

    public DateTime? ReceiptIssuedAtUtc { get; set; }

    public ICollection<PaymentAllocation> Allocations { get; set; } = new List<PaymentAllocation>();
}
