using System.ComponentModel.DataAnnotations;
using BuildingManagement.Core.Enums;

namespace BuildingManagement.Core.Entities.Finance;

/// <summary>
/// Represents a recurring standing order (subscription) for HOA payments.
/// Supports PayPal subscriptions and other provider recurring billing.
/// </summary>
public class StandingOrder
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public int UnitId { get; set; }
    public Unit Unit { get; set; } = null!;

    public int BuildingId { get; set; }
    public Building Building { get; set; } = null!;

    public PaymentProviderType ProviderType { get; set; }

    /// <summary>Provider subscription/agreement ID (e.g. PayPal subscription ID)</summary>
    [MaxLength(200)]
    public string? ProviderSubscriptionId { get; set; }

    /// <summary>Provider plan ID used for this subscription</summary>
    [MaxLength(200)]
    public string? ProviderPlanId { get; set; }

    public decimal Amount { get; set; }

    [MaxLength(3)]
    public string Currency { get; set; } = "ILS";

    public FrequencyType Frequency { get; set; } = FrequencyType.Monthly;

    public StandingOrderStatus Status { get; set; } = StandingOrderStatus.Active;

    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? NextChargeDate { get; set; }
    public DateTime? LastChargedAtUtc { get; set; }

    /// <summary>Approval URL for the tenant to approve the subscription (PayPal)</summary>
    [MaxLength(2000)]
    public string? ApprovalUrl { get; set; }

    public int SuccessfulCharges { get; set; }
    public int FailedCharges { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
}
