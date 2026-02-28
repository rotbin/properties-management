using System.ComponentModel.DataAnnotations;

namespace BuildingManagement.Core.Entities;

public class TenantMessage
{
    public int Id { get; set; }

    public int TenantProfileId { get; set; }
    public TenantProfile TenantProfile { get; set; } = null!;

    /// <summary>Null for AI-generated messages; set for manager-sent messages.</summary>
    public string? SentByUserId { get; set; }
    public ApplicationUser? SentByUser { get; set; }

    [Required, MaxLength(100)]
    public string Subject { get; set; } = string.Empty;

    [Required]
    public string Body { get; set; } = string.Empty;

    /// <summary>PaymentReminder, Manual, Warning</summary>
    [MaxLength(50)]
    public string MessageType { get; set; } = "Manual";

    /// <summary>GoodPayer, OccasionallyLate, ChronicallyLate, or null for manual messages.</summary>
    [MaxLength(50)]
    public string? PayerCategory { get; set; }

    public bool IsRead { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAtUtc { get; set; }
}
