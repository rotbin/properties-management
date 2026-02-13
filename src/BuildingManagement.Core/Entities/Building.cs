using System.ComponentModel.DataAnnotations;

namespace BuildingManagement.Core.Entities;

public class Building : BaseEntity
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? AddressLine { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    [MaxLength(20)]
    public string? PostalCode { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public ICollection<Unit> Units { get; set; } = new List<Unit>();
    public ICollection<Asset> Assets { get; set; } = new List<Asset>();
    public ICollection<CleaningPlan> CleaningPlans { get; set; } = new List<CleaningPlan>();
    public ICollection<ServiceRequest> ServiceRequests { get; set; } = new List<ServiceRequest>();
    public ICollection<WorkOrder> WorkOrders { get; set; } = new List<WorkOrder>();
}
