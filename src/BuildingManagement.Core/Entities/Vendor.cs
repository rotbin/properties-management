using System.ComponentModel.DataAnnotations;
using BuildingManagement.Core.Enums;

namespace BuildingManagement.Core.Entities;

public class Vendor : BaseEntity
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public VendorServiceType ServiceType { get; set; }

    [MaxLength(20)]
    public string? Phone { get; set; }

    [MaxLength(200)]
    public string? Email { get; set; }

    [MaxLength(200)]
    public string? ContactName { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public ICollection<Asset> Assets { get; set; } = new List<Asset>();
    public ICollection<WorkOrder> WorkOrders { get; set; } = new List<WorkOrder>();
}
