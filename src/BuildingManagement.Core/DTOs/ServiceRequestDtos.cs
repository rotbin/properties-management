using System.ComponentModel.DataAnnotations;
using BuildingManagement.Core.Enums;

namespace BuildingManagement.Core.DTOs;

public record ServiceRequestDto
{
    public int Id { get; init; }
    public int BuildingId { get; init; }
    public string? BuildingName { get; init; }
    public int? UnitId { get; init; }
    public string? UnitNumber { get; init; }
    public string SubmittedByUserId { get; init; } = string.Empty;
    public string SubmittedByName { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public Area Area { get; init; }
    public ServiceRequestCategory Category { get; init; }
    public Priority Priority { get; init; }
    public bool IsEmergency { get; init; }
    public string Description { get; init; } = string.Empty;
    public ServiceRequestStatus Status { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
    public List<AttachmentDto> Attachments { get; init; } = [];

    // Linked vendor assignment info (from WorkOrder)
    public int? AssignedVendorId { get; init; }
    public string? AssignedVendorName { get; init; }
    public int? LinkedWorkOrderId { get; init; }
    public string? LinkedWorkOrderStatus { get; init; }

    // Incident group
    public int? IncidentGroupId { get; init; }
    public string? IncidentGroupTitle { get; init; }
    public int IncidentTicketCount { get; init; }

    // Message count
    public int MessageCount { get; init; }
}

public record CreateServiceRequestRequest
{
    [Required]
    public int BuildingId { get; init; }

    public int? UnitId { get; init; }

    [Required, MaxLength(20)]
    public string Phone { get; init; } = string.Empty;

    public Area Area { get; init; }
    public ServiceRequestCategory Category { get; init; }
    public Priority Priority { get; init; }
    public bool IsEmergency { get; init; }

    [Required, MaxLength(2000)]
    public string Description { get; init; } = string.Empty;
}

public record UpdateServiceRequestStatusRequest
{
    [Required]
    public ServiceRequestStatus Status { get; init; }

    [MaxLength(500)]
    public string? Note { get; init; }
}

public record TicketMessageDto
{
    public int Id { get; init; }
    public int ServiceRequestId { get; init; }
    public string SenderType { get; init; } = string.Empty;
    public string? SenderUserId { get; init; }
    public string? SenderName { get; init; }
    public string Text { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
}

public record PostTicketMessageRequest
{
    [Required, MaxLength(4000)]
    public string Text { get; init; } = string.Empty;
}

public record AttachmentDto
{
    public int Id { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public DateTime UploadedAtUtc { get; init; }
}
