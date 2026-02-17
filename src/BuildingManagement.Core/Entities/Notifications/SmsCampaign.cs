using System.ComponentModel.DataAnnotations;
using BuildingManagement.Core.Enums;

namespace BuildingManagement.Core.Entities.Notifications;

public enum SmsCampaignStatus
{
    Draft = 0,
    Sent = 1,
    Cancelled = 2
}

public class SmsCampaign
{
    public int Id { get; set; }

    public int BuildingId { get; set; }
    public Building Building { get; set; } = null!;

    /// <summary>YYYY-MM format</summary>
    [Required, MaxLength(7)]
    public string Period { get; set; } = string.Empty;

    public int TemplateId { get; set; }
    public SmsTemplate Template { get; set; } = null!;

    [Required, MaxLength(450)]
    public string CreatedByUserId { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public SmsCampaignStatus Status { get; set; } = SmsCampaignStatus.Draft;

    public ReminderChannel Channel { get; set; } = ReminderChannel.Sms;

    [MaxLength(1000)]
    public string? Notes { get; set; }

    // Send result counts
    public int TotalSelected { get; set; }
    public int SentCount { get; set; }
    public int FailedCount { get; set; }
    public int SkippedCount { get; set; }
    public DateTime? SentAtUtc { get; set; }

    public ICollection<SmsCampaignRecipient> Recipients { get; set; } = new List<SmsCampaignRecipient>();
}
