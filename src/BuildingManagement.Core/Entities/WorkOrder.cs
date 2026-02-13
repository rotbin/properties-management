using System.ComponentModel.DataAnnotations;
using BuildingManagement.Core.Enums;

namespace BuildingManagement.Core.Entities;

public class WorkOrder : BaseEntity
{
    public int BuildingId { get; set; }
    public Building Building { get; set; } = null!;

    public int? ServiceRequestId { get; set; }
    public ServiceRequest? ServiceRequest { get; set; }

    public int? VendorId { get; set; }
    public Vendor? Vendor { get; set; }

    [Required, MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    public DateTime? ScheduledFor { get; set; }

    public WorkOrderStatus Status { get; set; } = WorkOrderStatus.Draft;

    public DateTime? CompletedAtUtc { get; set; }

    public ICollection<WorkOrderNote> Notes { get; set; } = new List<WorkOrderNote>();
    public ICollection<WorkOrderAttachment> Attachments { get; set; } = new List<WorkOrderAttachment>();
}

public class WorkOrderNote
{
    public int Id { get; set; }
    public int WorkOrderId { get; set; }
    public WorkOrder WorkOrder { get; set; } = null!;

    [Required, MaxLength(2000)]
    public string NoteText { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [Required]
    public string CreatedByUserId { get; set; } = string.Empty;
    public ApplicationUser CreatedByUser { get; set; } = null!;
}

public class WorkOrderAttachment
{
    public int Id { get; set; }
    public int WorkOrderId { get; set; }
    public WorkOrder WorkOrder { get; set; } = null!;

    [Required, MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string ContentType { get; set; } = string.Empty;

    [Required, MaxLength(1000)]
    public string StoredPath { get; set; } = string.Empty;

    public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;
}
