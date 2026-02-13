using System.Text;
using System.Text.Json;
using BuildingManagement.Core.Enums;
using BuildingManagement.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace BuildingManagement.Infrastructure.Services.Gateways;

/// <summary>
/// Fully working fake payment gateway for local development and testing.
/// Simulates hosted payment pages, tokenization, and charging without a real provider.
/// </summary>
public class FakePaymentGateway : IPaymentGateway
{
    private readonly ILogger<FakePaymentGateway> _logger;

    public FakePaymentGateway(ILogger<FakePaymentGateway> logger)
    {
        _logger = logger;
    }

    public string ProviderName => "Fake (Development)";
    public PaymentProviderType ProviderType => PaymentProviderType.Fake;

    public Task<PaymentSessionResult> CreatePaymentSessionAsync(CreatePaymentSessionRequest req, CancellationToken ct = default)
    {
        var sessionId = $"fake_sess_{Guid.NewGuid():N}";
        var providerRef = $"fake_pay_{Guid.NewGuid():N}";

        // For Fake gateway, immediately create a "succeeded" session and return success URL with params
        var paymentUrl = $"{req.SuccessUrl}?session_id={sessionId}&provider_ref={providerRef}&status=succeeded";

        _logger.LogInformation("FAKE: Created payment session {Session} for {Amount} {Currency}, redirect -> {Url}",
            sessionId, req.Amount, req.Currency, paymentUrl);

        return Task.FromResult(new PaymentSessionResult(true, paymentUrl, sessionId, providerRef));
    }

    public Task<TokenizeResult> TokenizePaymentMethodAsync(TokenizeRequest req, CancellationToken ct = default)
    {
        var token = $"fake_tok_{Guid.NewGuid():N}";
        var last4 = "1111";
        var expiry = "12/28";
        var redirectUrl = $"{req.SuccessUrl}?token={token}&last4={last4}&expiry={expiry}&brand=Visa&customer_id=fake_cust_{req.UserId[..Math.Min(8, req.UserId.Length)]}";

        _logger.LogInformation("FAKE: Tokenization session created for user {UserId}, redirect -> {Url}", req.UserId, redirectUrl);

        return Task.FromResult(new TokenizeResult(true, redirectUrl, token, last4, expiry, "Visa", $"fake_cust_{req.UserId[..Math.Min(8, req.UserId.Length)]}"));
    }

    public Task<ChargeResult> ChargeTokenAsync(ChargeTokenRequest req, CancellationToken ct = default)
    {
        if (req.Token.Contains("_fail_"))
        {
            _logger.LogWarning("FAKE: Charge FAILED (simulated) for token {Token}, amount {Amount}", req.Token, req.Amount);
            return Task.FromResult(new ChargeResult(false, null, "Simulated payment failure"));
        }

        var providerRef = $"fake_ch_{Guid.NewGuid():N}";
        _logger.LogInformation("FAKE: Charged {Amount} {Currency} on token {Token} -> {Ref}", req.Amount, req.Currency, req.Token, providerRef);
        return Task.FromResult(new ChargeResult(true, providerRef));
    }

    public Task<RefundResult> RefundAsync(RefundRequest req, CancellationToken ct = default)
    {
        var refundRef = $"fake_ref_{Guid.NewGuid():N}";
        _logger.LogInformation("FAKE: Refunded {Amount} {Currency} for {ProviderRef}", req.Amount, req.Currency, req.ProviderReference);
        return Task.FromResult(new RefundResult(true, refundRef));
    }

    public async Task<WebhookParseResult> ParseWebhookAsync(Stream body, IDictionary<string, string> headers, CancellationToken ct = default)
    {
        using var reader = new StreamReader(body, Encoding.UTF8);
        var payload = await reader.ReadToEndAsync(ct);
        _logger.LogInformation("FAKE: Webhook received, payload length {Len}", payload.Length);

        try
        {
            var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            var eventId = root.TryGetProperty("eventId", out var eid) ? eid.GetString() : $"fake_evt_{Guid.NewGuid():N}";
            var providerRef = root.TryGetProperty("providerReference", out var pr) ? pr.GetString() : null;
            var status = root.TryGetProperty("status", out var st) && st.GetString() == "succeeded"
                ? PaymentStatus.Succeeded
                : PaymentStatus.Failed;
            var token = root.TryGetProperty("token", out var tk) ? tk.GetString() : null;
            var last4 = root.TryGetProperty("last4", out var l4) ? l4.GetString() : null;

            return new WebhookParseResult(true, eventId, providerRef, status, token, last4, null, null, null, null);
        }
        catch
        {
            return new WebhookParseResult(true, $"fake_evt_{Guid.NewGuid():N}", null, PaymentStatus.Succeeded, null, null, null, null, null, null);
        }
    }

    public Task<bool> VerifyWebhookSignatureAsync(WebhookParseResult parsed, IDictionary<string, string> headers, CancellationToken ct = default)
    {
        // Fake gateway: always valid
        return Task.FromResult(true);
    }
}
