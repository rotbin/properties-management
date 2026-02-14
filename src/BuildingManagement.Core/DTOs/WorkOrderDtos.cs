using System.ComponentModel.DataAnnotations;
using BuildingManagement.Core.Enums;

namespace BuildingManagement.Core.DTOs;

public record WorkOrderDto
{
    public int Id { get; init; }
    public int BuildingId { get; init; }
    public string? BuildingName { get; init; }
    public string? BuildingAddress { get; init; }
    public int? ServiceRequestId { get; init; }
    public int? VendorId { get; init; }
    public string? VendorName { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public DateTime? ScheduledFor { get; init; }
    public WorkOrderStatus Status { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
    public DateTime? CompletedAtUtc { get; init; }
    public List<WorkOrderNoteDto> Notes { get; init; } = [];
    public List<AttachmentDto> Attachments { get; init; } = [];

    // SR details (populated when WO is linked to an SR)
    public string? SrArea { get; init; }
    public string? SrCategory { get; init; }
    public string? SrPriority { get; init; }
    public bool SrIsEmergency { get; init; }
    public string? SrPhone { get; init; }
    public string? SrSubmittedByName { get; init; }
    public string? SrDescription { get; init; }
    public List<AttachmentDto> SrAttachments { get; init; } = [];
}

public record CreateWorkOrderRequest
{
    [Required]
    public int BuildingId { get; init; }

    public int? ServiceRequestId { get; init; }
    public int? VendorId { get; init; }

    [Required, MaxLength(300)]
    public string Title { get; init; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; init; }

    public DateTime? ScheduledFor { get; init; }
}

public record AssignWorkOrderRequest
{
    [Required]
    public int VendorId { get; init; }
    public DateTime? ScheduledFor { get; init; }
}

public record AssignVendorToSrRequest
{
    [Required]
    public int VendorId { get; init; }
    public DateTime? ScheduledFor { get; init; }
    [MaxLength(300)]
    public string? Title { get; init; }
    [MaxLength(2000)]
    public string? Notes { get; init; }
}

public record UpdateWorkOrderStatusRequest
{
    [Required]
    public WorkOrderStatus Status { get; init; }
}

public record CreateWorkOrderNoteRequest
{
    [Required, MaxLength(2000)]
    public string NoteText { get; init; } = string.Empty;
}

public record WorkOrderNoteDto
{
    public int Id { get; init; }
    public string NoteText { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
    public string CreatedByUserId { get; init; } = string.Empty;
    public string? CreatedByName { get; init; }
}
