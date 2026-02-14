using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BuildingManagement.Core.Enums;

namespace BuildingManagement.Core.Entities.Finance;

public class VendorInvoice : BaseEntity
{
    public int BuildingId { get; set; }
    public Building Building { get; set; } = null!;

    public int VendorId { get; set; }
    public Vendor Vendor { get; set; } = null!;

    public int? WorkOrderId { get; set; }
    public WorkOrder? WorkOrder { get; set; }

    public int? ServiceRequestId { get; set; }

    [MaxLength(100)]
    public string? Category { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(100)]
    public string? InvoiceNumber { get; set; }

    public DateTime InvoiceDate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    public DateTime? DueDate { get; set; }

    public VendorInvoiceStatus Status { get; set; } = VendorInvoiceStatus.Draft;

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public ICollection<VendorPayment> Payments { get; set; } = new List<VendorPayment>();
}

public class VendorPayment
{
    public int Id { get; set; }

    public int VendorInvoiceId { get; set; }
    public VendorInvoice VendorInvoice { get; set; } = null!;

    [Column(TypeName = "decimal(18,2)")]
    public decimal PaidAmount { get; set; }

    public DateTime PaidAtUtc { get; set; }

    public VendorPaymentMethod PaymentMethod { get; set; }

    [MaxLength(200)]
    public string? Reference { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
