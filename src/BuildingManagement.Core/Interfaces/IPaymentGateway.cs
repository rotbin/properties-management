using BuildingManagement.Core.Enums;

namespace BuildingManagement.Core.Interfaces;

// ─── Request / Result records ────────────────────────────

public record CreatePaymentSessionRequest(
    int BuildingId,
    int UnitChargeId,
    string UserId,
    string UserEmail,
    string UserName,
    decimal Amount,
    string Currency,
    string Description,
    string SuccessUrl,
    string CancelUrl,
    string WebhookUrl,
    string? IdempotencyKey = null);

public record PaymentSessionResult(
    bool Success,
    string? PaymentUrl,
    string? SessionId,
    string? ProviderReference,
    string? Error = null);

public record TokenizeRequest(
    int BuildingId,
    string UserId,
    string UserEmail,
    string UserName,
    string SuccessUrl,
    string CancelUrl,
    string WebhookUrl);

public record TokenizeResult(
    bool Success,
    string? RedirectUrl,
    string? Token,
    string? Last4,
    string? Expiry,
    string? CardBrand,
    string? ProviderCustomerId,
    string? Error = null);

public record ChargeTokenRequest(
    int BuildingId,
    string Token,
    decimal Amount,
    string Currency,
    string Description,
    string? IdempotencyKey = null);

public record ChargeResult(
    bool Success,
    string? ProviderReference,
    string? Error = null);

public record RefundRequest(
    int BuildingId,
    string ProviderReference,
    decimal Amount,
    string Currency,
    string? Reason = null);

public record RefundResult(
    bool Success,
    string? RefundReference,
    string? Error = null);

public record WebhookParseResult(
    bool Parsed,
    string? EventId,
    string? ProviderReference,
    PaymentStatus? Status,
    string? Token,
    string? Last4,
    string? Expiry,
    string? CardBrand,
    string? ProviderCustomerId,
    string? RawSignature,
    string? Error = null);

// ─── Gateway interface ──────────────────────────────────

public interface IPaymentGateway
{
    string ProviderName { get; }
    PaymentProviderType ProviderType { get; }

    /// <summary>Create a hosted payment session — returns a URL the client redirects to.</summary>
    Task<PaymentSessionResult> CreatePaymentSessionAsync(CreatePaymentSessionRequest req, CancellationToken ct = default);

    /// <summary>Start tokenization flow — returns a redirect URL for the hosted page.</summary>
    Task<TokenizeResult> TokenizePaymentMethodAsync(TokenizeRequest req, CancellationToken ct = default);

    /// <summary>Charge a previously tokenized payment method (for recurring / standing orders).</summary>
    Task<ChargeResult> ChargeTokenAsync(ChargeTokenRequest req, CancellationToken ct = default);

    /// <summary>Refund a previous charge (optional for MVP — may return NotSupported).</summary>
    Task<RefundResult> RefundAsync(RefundRequest req, CancellationToken ct = default);

    /// <summary>Parse an incoming webhook payload into a normalized result.</summary>
    Task<WebhookParseResult> ParseWebhookAsync(Stream body, IDictionary<string, string> headers, CancellationToken ct = default);

    /// <summary>Verify webhook signature. Returns false if verification fails. Fail-closed in production.</summary>
    Task<bool> VerifyWebhookSignatureAsync(WebhookParseResult parsed, IDictionary<string, string> headers, CancellationToken ct = default);
}

// ─── Factory ────────────────────────────────────────────

public interface IPaymentGatewayFactory
{
    /// <summary>Get the gateway for a specific building (reads PaymentProviderConfig). Falls back to global default.</summary>
    Task<IPaymentGateway> GetGatewayAsync(int? buildingId = null, CancellationToken ct = default);

    /// <summary>Get gateway by explicit provider type.</summary>
    IPaymentGateway GetGateway(PaymentProviderType providerType);
}
