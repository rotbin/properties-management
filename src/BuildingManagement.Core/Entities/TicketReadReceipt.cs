using System.ComponentModel.DataAnnotations;

namespace BuildingManagement.Core.Entities;

public class TicketReadReceipt
{
    public int Id { get; set; }

    public int ServiceRequestId { get; set; }

    [Required, MaxLength(450)]
    public string UserId { get; set; } = string.Empty;

    public int LastReadMessageId { get; set; }

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
