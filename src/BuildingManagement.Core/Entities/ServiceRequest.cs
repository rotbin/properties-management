using System.ComponentModel.DataAnnotations;
using BuildingManagement.Core.Enums;

namespace BuildingManagement.Core.Entities;

public class ServiceRequest : BaseEntity
{
    public int BuildingId { get; set; }
    public Building Building { get; set; } = null!;

    public int? UnitId { get; set; }
    public Unit? Unit { get; set; }

    [Required]
    public string SubmittedByUserId { get; set; } = string.Empty;
    public ApplicationUser SubmittedByUser { get; set; } = null!;

    [MaxLength(200)]
    public string SubmittedByName { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? Phone { get; set; }

    [MaxLength(200)]
    public string? Email { get; set; }

    public Area Area { get; set; }
    public ServiceRequestCategory Category { get; set; }
    public Priority Priority { get; set; }
    public bool IsEmergency { get; set; }

    [Required, MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    public ServiceRequestStatus Status { get; set; } = ServiceRequestStatus.New;

    public ICollection<ServiceRequestAttachment> Attachments { get; set; } = new List<ServiceRequestAttachment>();
    public ICollection<WorkOrder> WorkOrders { get; set; } = new List<WorkOrder>();
}
