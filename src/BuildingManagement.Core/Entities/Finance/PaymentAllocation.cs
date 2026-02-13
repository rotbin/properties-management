using System.ComponentModel.DataAnnotations.Schema;

namespace BuildingManagement.Core.Entities.Finance;

public class PaymentAllocation
{
    public int Id { get; set; }

    public int PaymentId { get; set; }
    public Payment Payment { get; set; } = null!;

    public int UnitChargeId { get; set; }
    public UnitCharge UnitCharge { get; set; } = null!;

    [Column(TypeName = "decimal(18,2)")]
    public decimal AllocatedAmount { get; set; }
}
