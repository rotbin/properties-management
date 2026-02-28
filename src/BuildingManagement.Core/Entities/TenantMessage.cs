using System.ComponentModel.DataAnnotations;

namespace BuildingManagement.Core.Entities;

public class TenantMessage
{
    public int Id { get; set; }

    public int TenantProfileId { get; set; }
    public TenantProfile TenantProfile { get; set; } = null!;

    /// <summary>Null for AI-generated messages; set for manager-sent or tenant-sent messages.</summary>
    public string? SentByUserId { get; set; }
    public ApplicationUser? SentByUser { get; set; }

    /// <summary>Null for root messages; set for replies to link them to the original.</summary>
    public int? ParentMessageId { get; set; }
    public TenantMessage? ParentMessage { get; set; }
    public ICollection<TenantMessage> Replies { get; set; } = new List<TenantMessage>();

    [Required, MaxLength(100)]
    public string Subject { get; set; } = string.Empty;

    [Required]
    public string Body { get; set; } = string.Empty;

    /// <summary>PaymentReminder, Manual, Warning, TenantReply, ManagerReply</summary>
    [MaxLength(50)]
    public string MessageType { get; set; } = "Manual";

    /// <summary>GoodPayer, OccasionallyLate, ChronicallyLate, or null for manual messages.</summary>
    [MaxLength(50)]
    public string? PayerCategory { get; set; }

    public bool IsRead { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAtUtc { get; set; }
}
