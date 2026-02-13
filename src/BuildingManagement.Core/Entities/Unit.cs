using System.ComponentModel.DataAnnotations;

namespace BuildingManagement.Core.Entities;

public class Unit : BaseEntity
{
    public int BuildingId { get; set; }
    public Building Building { get; set; } = null!;

    [Required, MaxLength(20)]
    public string UnitNumber { get; set; } = string.Empty;

    public int? Floor { get; set; }
    public decimal? SizeSqm { get; set; }

    [MaxLength(200)]
    public string? OwnerName { get; set; }

    // Tenant user linked to this unit
    public string? TenantUserId { get; set; }
    public ApplicationUser? TenantUser { get; set; }
}
