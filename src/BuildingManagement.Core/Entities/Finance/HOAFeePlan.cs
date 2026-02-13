using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BuildingManagement.Core.Enums;

namespace BuildingManagement.Core.Entities.Finance;

public class HOAFeePlan : BaseEntity
{
    public int BuildingId { get; set; }
    public Building Building { get; set; } = null!;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public HOACalculationMethod CalculationMethod { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? AmountPerSqm { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? FixedAmountPerUnit { get; set; }

    public DateTime EffectiveFrom { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<UnitCharge> UnitCharges { get; set; } = new List<UnitCharge>();
}
