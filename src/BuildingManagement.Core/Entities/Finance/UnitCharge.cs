using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BuildingManagement.Core.Enums;

namespace BuildingManagement.Core.Entities.Finance;

public class UnitCharge
{
    public int Id { get; set; }

    public int UnitId { get; set; }
    public Unit Unit { get; set; } = null!;

    public int HOAFeePlanId { get; set; }
    public HOAFeePlan HOAFeePlan { get; set; } = null!;

    /// <summary>YYYY-MM format</summary>
    [Required, MaxLength(7)]
    public string Period { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal AmountDue { get; set; }

    public DateTime DueDate { get; set; }

    public UnitChargeStatus Status { get; set; } = UnitChargeStatus.Pending;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<PaymentAllocation> Allocations { get; set; } = new List<PaymentAllocation>();
}
