using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BuildingManagement.Core.Enums;

namespace BuildingManagement.Core.Entities.Finance;

public class LedgerEntry
{
    public int Id { get; set; }

    public int BuildingId { get; set; }
    public Building Building { get; set; } = null!;

    public int? UnitId { get; set; }
    public Unit? Unit { get; set; }

    public LedgerEntryType EntryType { get; set; }

    /// <summary>FK to Charge / Payment / etc.</summary>
    public int? ReferenceId { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Debit { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Credit { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal BalanceAfter { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
