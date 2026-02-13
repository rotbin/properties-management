using System.ComponentModel.DataAnnotations;
using BuildingManagement.Core.Enums;

namespace BuildingManagement.Core.Entities.Finance;

/// <summary>
/// Tracks processed webhook events to ensure idempotency.
/// </summary>
public class WebhookEventLog
{
    public int Id { get; set; }

    public PaymentProviderType ProviderType { get; set; }

    /// <summary>Provider's unique event identifier</summary>
    [Required, MaxLength(500)]
    public string EventId { get; set; } = string.Empty;

    public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>SHA-256 hash of payload for dedup</summary>
    [MaxLength(64)]
    public string? PayloadHash { get; set; }

    public DateTime? ProcessedAtUtc { get; set; }

    [MaxLength(50)]
    public string? Result { get; set; }
}
