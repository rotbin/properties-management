using System.ComponentModel.DataAnnotations;
using BuildingManagement.Core.Enums;

namespace BuildingManagement.Core.Entities;

public class Asset : BaseEntity
{
    public int BuildingId { get; set; }
    public Building Building { get; set; } = null!;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public AssetType AssetType { get; set; }

    [MaxLength(500)]
    public string? LocationDescription { get; set; }

    [MaxLength(100)]
    public string? SerialNumber { get; set; }

    public DateTime? InstallDate { get; set; }
    public DateTime? WarrantyUntil { get; set; }

    public int? VendorId { get; set; }
    public Vendor? Vendor { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public ICollection<PreventivePlan> PreventivePlans { get; set; } = new List<PreventivePlan>();
}
