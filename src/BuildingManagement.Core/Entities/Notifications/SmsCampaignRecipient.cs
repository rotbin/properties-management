using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BuildingManagement.Core.Entities.Notifications;

public enum SmsSendStatus
{
    Pending = 0,
    Sent = 1,
    Failed = 2,
    Skipped = 3
}

public class SmsCampaignRecipient
{
    public int Id { get; set; }

    public int CampaignId { get; set; }
    public SmsCampaign Campaign { get; set; } = null!;

    public int UnitId { get; set; }

    public int? TenantProfileId { get; set; }

    [MaxLength(200)]
    public string FullNameSnapshot { get; set; } = string.Empty;

    [MaxLength(30)]
    public string? PhoneSnapshot { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal AmountDueSnapshot { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal AmountPaidSnapshot { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal OutstandingSnapshot { get; set; }

    [MaxLength(20)]
    public string ChargeStatusSnapshot { get; set; } = string.Empty;

    public bool IsSelected { get; set; } = true;

    public SmsSendStatus SendStatus { get; set; } = SmsSendStatus.Pending;

    [MaxLength(200)]
    public string? ProviderMessageId { get; set; }

    [MaxLength(500)]
    public string? ErrorMessage { get; set; }

    public DateTime? SentAtUtc { get; set; }
}
