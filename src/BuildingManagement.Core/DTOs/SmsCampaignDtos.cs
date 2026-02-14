using System.ComponentModel.DataAnnotations;
using BuildingManagement.Core.Entities.Notifications;

namespace BuildingManagement.Core.DTOs;

// ─── SMS Templates ──────────────────────────────────────

public record SmsTemplateDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Language { get; init; } = "he";
    public string Body { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}

// ─── SMS Campaign ───────────────────────────────────────

public record CreateSmsCampaignRequest
{
    [Required]
    public int BuildingId { get; init; }

    [Required, MaxLength(7)]
    public string Period { get; init; } = string.Empty;

    [Required]
    public int TemplateId { get; init; }

    public bool IncludePartial { get; init; } = true;

    [MaxLength(1000)]
    public string? Notes { get; init; }
}

public record SmsCampaignDto
{
    public int Id { get; init; }
    public int BuildingId { get; init; }
    public string? BuildingName { get; init; }
    public string Period { get; init; } = string.Empty;
    public int TemplateId { get; init; }
    public string? TemplateName { get; init; }
    public string CreatedByUserId { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
    public SmsCampaignStatus Status { get; init; }
    public string? Notes { get; init; }
    public int TotalSelected { get; init; }
    public int SentCount { get; init; }
    public int FailedCount { get; init; }
    public int SkippedCount { get; init; }
    public DateTime? SentAtUtc { get; init; }
}

public record SmsCampaignRecipientDto
{
    public int Id { get; init; }
    public int UnitId { get; init; }
    public int? TenantProfileId { get; init; }
    public string FullNameSnapshot { get; init; } = string.Empty;
    public string? PhoneSnapshot { get; init; }
    public decimal AmountDueSnapshot { get; init; }
    public decimal AmountPaidSnapshot { get; init; }
    public decimal OutstandingSnapshot { get; init; }
    public string ChargeStatusSnapshot { get; init; } = string.Empty;
    public bool IsSelected { get; init; }
    public SmsSendStatus SendStatus { get; init; }
    public string? ProviderMessageId { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime? SentAtUtc { get; init; }
}

public record CreateCampaignResult
{
    public SmsCampaignDto Campaign { get; init; } = null!;
    public List<SmsCampaignRecipientDto> Recipients { get; init; } = [];
}

// ─── Recipient Updates ──────────────────────────────────

public record RecipientSelectionUpdate
{
    public int RecipientId { get; init; }
    public bool IsSelected { get; init; }
}

public record UpdateRecipientsRequest
{
    public List<RecipientSelectionUpdate>? Updates { get; init; }
    public List<int>? AddUnitIds { get; init; }
    public List<int>? RemoveRecipientIds { get; init; }
}

// ─── Send ───────────────────────────────────────────────

public record SendCampaignRequest
{
    public bool Confirm { get; init; }
}

public record SendCampaignResult
{
    public int TotalSelected { get; init; }
    public int SentCount { get; init; }
    public int FailedCount { get; init; }
    public int SkippedCount { get; init; }
}
