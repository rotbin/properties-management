using System.ComponentModel.DataAnnotations;
using BuildingManagement.Core.Enums;

namespace BuildingManagement.Core.Entities;

public class TicketMessage
{
    public int Id { get; set; }

    public int ServiceRequestId { get; set; }
    public ServiceRequest ServiceRequest { get; set; } = null!;

    public TicketMessageSender SenderType { get; set; }

    [MaxLength(450)]
    public string? SenderUserId { get; set; }

    [MaxLength(200)]
    public string? SenderName { get; set; }

    [Required, MaxLength(4000)]
    public string Text { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
