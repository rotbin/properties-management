using System.ComponentModel.DataAnnotations;
using BuildingManagement.Core.Enums;

namespace BuildingManagement.Core.DTOs;

// ─── HOA Fee Plans ──────────────────────────────────────

public record HOAFeePlanDto
{
    public int Id { get; init; }
    public int BuildingId { get; init; }
    public string? BuildingName { get; init; }
    public string Name { get; init; } = string.Empty;
    public HOACalculationMethod CalculationMethod { get; init; }
    public decimal? AmountPerSqm { get; init; }
    public decimal? FixedAmountPerUnit { get; init; }
    public DateTime EffectiveFrom { get; init; }
    public bool IsActive { get; init; }
}

public record CreateHOAFeePlanRequest
{
    [Required]
    public int BuildingId { get; init; }

    [Required, MaxLength(200)]
    public string Name { get; init; } = string.Empty;

    public HOACalculationMethod CalculationMethod { get; init; }
    public decimal? AmountPerSqm { get; init; }
    public decimal? FixedAmountPerUnit { get; init; }
    public DateTime EffectiveFrom { get; init; }
}

public record UpdateHOAFeePlanRequest
{
    [Required, MaxLength(200)]
    public string Name { get; init; } = string.Empty;

    public HOACalculationMethod CalculationMethod { get; init; }
    public decimal? AmountPerSqm { get; init; }
    public decimal? FixedAmountPerUnit { get; init; }
    public bool IsActive { get; init; }
}

// ─── Unit Charges ───────────────────────────────────────

public record UnitChargeDto
{
    public int Id { get; init; }
    public int UnitId { get; init; }
    public string? UnitNumber { get; init; }
    public int? Floor { get; init; }
    public string? TenantName { get; init; }
    public int HOAFeePlanId { get; init; }
    public string Period { get; init; } = string.Empty;
    public decimal AmountDue { get; init; }
    public decimal AmountPaid { get; init; }
    public decimal Balance { get; init; }
    public DateTime DueDate { get; init; }
    public UnitChargeStatus Status { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

public record AdjustChargeRequest
{
    [Required]
    public decimal NewAmount { get; init; }

    [MaxLength(500)]
    public string? Reason { get; init; }
}

// ─── Payment Methods ────────────────────────────────────

public record PaymentMethodDto
{
    public int Id { get; init; }
    public PaymentMethodType MethodType { get; init; }
    public string? Provider { get; init; }
    public string? Last4Digits { get; init; }
    public string? Expiry { get; init; }
    public string? CardBrand { get; init; }
    public bool IsDefault { get; init; }
    public bool IsActive { get; init; }
}

public record SetupPaymentMethodRequest
{
    public PaymentMethodType MethodType { get; init; }

    [MaxLength(50)]
    public string? Provider { get; init; }

    /// <summary>For Fake gateway only: card number (tokenized, never stored). For real providers this is unused — use hosted flow.</summary>
    public string? CardNumber { get; init; }
    public string? Expiry { get; init; }
    public string? Cvv { get; init; }
    public bool IsDefault { get; init; }
}

// ─── Hosted Payment Flow ────────────────────────────────

public record CreatePaymentSessionResponse
{
    public string? PaymentUrl { get; init; }
    public string? SessionId { get; init; }
    public int? PaymentId { get; init; }
    public string? Error { get; init; }
}

public record StartTokenizationResponse
{
    public string? RedirectUrl { get; init; }
    public string? Error { get; init; }
}

public record StartTokenizationRequest
{
    public int BuildingId { get; init; }
    public bool IsDefault { get; init; } = true;
}

// ─── Provider Configuration ─────────────────────────────

public record PaymentProviderConfigDto
{
    public int Id { get; init; }
    public int? BuildingId { get; init; }
    public string? BuildingName { get; init; }
    public string ProviderType { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public string? MerchantIdRef { get; init; }
    public string? TerminalIdRef { get; init; }
    public string? ApiUserRef { get; init; }
    public string? ApiPasswordRef { get; init; }
    public string? WebhookSecretRef { get; init; }
    public int SupportedFeatures { get; init; }
    public string Currency { get; init; } = "ILS";
    public string? BaseUrl { get; init; }
}

public record CreatePaymentProviderConfigRequest
{
    public int? BuildingId { get; init; }

    [Required]
    public string ProviderType { get; init; } = string.Empty;

    public string? MerchantIdRef { get; init; }
    public string? TerminalIdRef { get; init; }
    public string? ApiUserRef { get; init; }
    public string? ApiPasswordRef { get; init; }
    public string? WebhookSecretRef { get; init; }
    public int SupportedFeatures { get; init; }
    public string Currency { get; init; } = "ILS";
    public string? BaseUrl { get; init; }
}

// ─── Payments ───────────────────────────────────────────

public record PaymentDto
{
    public int Id { get; init; }
    public int UnitId { get; init; }
    public string? UnitNumber { get; init; }
    public string UserId { get; init; } = string.Empty;
    public string? UserName { get; init; }
    public decimal Amount { get; init; }
    public DateTime PaymentDateUtc { get; init; }
    public int? PaymentMethodId { get; init; }
    public string? Last4 { get; init; }
    public string? ProviderReference { get; init; }
    public PaymentStatus Status { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

public record PayChargeRequest
{
    public int? PaymentMethodId { get; init; }
    public decimal? Amount { get; init; }
}

// ─── Reports: Collection Status (Who Paid / Who Has Not) ─────

public record CollectionRowDto
{
    public int UnitId { get; init; }
    public string UnitNumber { get; init; } = string.Empty;
    public int? Floor { get; init; }
    public decimal? SizeSqm { get; init; }
    public string? PayerDisplayName { get; init; }
    public string? PayerPhone { get; init; }
    public decimal AmountDue { get; init; }
    public decimal AmountPaid { get; init; }
    public decimal Outstanding { get; init; }
    public DateTime? DueDate { get; init; }
    public string Status { get; init; } = string.Empty; // Paid, Partial, Unpaid, Overdue, NotGenerated
    public DateTime? LastPaymentDateUtc { get; init; }
}

public record CollectionSummaryDto
{
    public int BuildingId { get; init; }
    public string? BuildingName { get; init; }
    public string Period { get; init; } = string.Empty;
    public int TotalUnits { get; init; }
    public int GeneratedCount { get; init; }
    public int PaidCount { get; init; }
    public int PartialCount { get; init; }
    public int UnpaidCount { get; init; }
    public int OverdueCount { get; init; }
    public decimal TotalDue { get; init; }
    public decimal TotalPaid { get; init; }
    public decimal TotalOutstanding { get; init; }
    public decimal CollectionRatePercent { get; init; }
}

public record CollectionStatusReport
{
    public CollectionSummaryDto Summary { get; init; } = null!;
    public List<CollectionRowDto> Rows { get; init; } = [];
}

// Legacy DTO kept for backward compat (used by old CSV)
public record CollectionStatusRow
{
    public int UnitId { get; init; }
    public string UnitNumber { get; init; } = string.Empty;
    public int? Floor { get; init; }
    public string? ResidentName { get; init; }
    public decimal AmountDue { get; init; }
    public decimal AmountPaid { get; init; }
    public decimal Balance { get; init; }
    public string Status { get; init; } = string.Empty;
}

public record AgingBucket
{
    public int UnitId { get; init; }
    public string UnitNumber { get; init; } = string.Empty;
    public string? ResidentName { get; init; }
    public decimal Current { get; init; }
    public decimal Days1to30 { get; init; }
    public decimal Days31to60 { get; init; }
    public decimal Days61to90 { get; init; }
    public decimal Days90Plus { get; init; }
    public decimal Total { get; init; }
}

public record AgingReport
{
    public int BuildingId { get; init; }
    public string? BuildingName { get; init; }
    public List<AgingBucket> Buckets { get; init; } = [];
    public decimal GrandTotal { get; init; }
}

// ─── Income vs Expenses Report ─────────────────────────

public record CategoryAmount
{
    public string Category { get; init; } = string.Empty;
    public decimal Amount { get; init; }
}

public record MonthlyBreakdown
{
    public string Month { get; init; } = string.Empty;
    public decimal Income { get; init; }
    public decimal Expenses { get; init; }
    public decimal Net { get; init; }
}

public record IncomeExpensesReport
{
    public int BuildingId { get; init; }
    public string? BuildingName { get; init; }
    public DateTime FromDate { get; init; }
    public DateTime ToDate { get; init; }
    public decimal TotalIncome { get; init; }
    public decimal TotalExpenses { get; init; }
    public decimal NetBalance { get; init; }
    public List<CategoryAmount> IncomeByCategory { get; init; } = [];
    public List<CategoryAmount> ExpensesByCategory { get; init; } = [];
    public List<MonthlyBreakdown> MonthlyBreakdown { get; init; } = [];
}
