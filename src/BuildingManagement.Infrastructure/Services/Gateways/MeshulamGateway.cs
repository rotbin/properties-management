using System.Text;
using BuildingManagement.Core.Enums;
using BuildingManagement.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace BuildingManagement.Infrastructure.Services.Gateways;

/// <summary>
/// Meshulam (משולם) payment gateway integration.
/// API Docs: https://meshulam.co.il/developers
/// 
/// Meshulam supports:
///   - Hosted payment page (iframe / redirect)
///   - Tokenization for recurring charges
///   - Webhook notifications
///   - Refunds
///
/// SECURITY: All API credentials must be stored in Azure Key Vault.
/// This class reads Key Vault secret REFERENCES from PaymentProviderConfig
/// and resolves them at runtime.
/// </summary>
public class MeshulamGateway : IPaymentGateway
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MeshulamGateway> _logger;

    // TODO: Set from PaymentProviderConfig.BaseUrl or default
    private const string SandboxBaseUrl = "https://sandbox.meshulam.co.il/api/light/server/1.0";
    private const string ProductionBaseUrl = "https://secure.meshulam.co.il/api/light/server/1.0";

    public MeshulamGateway(HttpClient httpClient, ILogger<MeshulamGateway> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public string ProviderName => "Meshulam (משולם)";
    public PaymentProviderType ProviderType => PaymentProviderType.Meshulam;

    public async Task<PaymentSessionResult> CreatePaymentSessionAsync(CreatePaymentSessionRequest req, CancellationToken ct = default)
    {
        // TODO: Implement with real Meshulam API
        // Endpoint: POST /createPaymentProcess
        // Required fields:
        //   - pageCode: from merchant dashboard
        //   - userId: Meshulam user ID (from Key Vault via MerchantIdRef)
        //   - apiKey: Meshulam API key (from Key Vault via ApiPasswordRef)
        //   - sum: amount in agorot (multiply by 100)
        //   - description: payment description
        //   - pageField[fullName]: payer name
        //   - pageField[email]: payer email
        //   - successUrl, cancelUrl
        //   - cField1: internal reference (unitChargeId)
        //
        // Response: { status: 1, data: { url: "https://...", processId: "..." } }

        _logger.LogWarning("MeshulamGateway.CreatePaymentSessionAsync called but not yet implemented with real credentials");
        return new PaymentSessionResult(false, null, null, null, "Meshulam gateway not configured. Add merchant credentials in provider settings.");
    }

    public async Task<TokenizeResult> TokenizePaymentMethodAsync(TokenizeRequest req, CancellationToken ct = default)
    {
        // TODO: Implement with real Meshulam API
        // Endpoint: POST /createPaymentProcess with tokenization flag
        // Meshulam returns a hosted page URL where user enters card details
        // On completion, Meshulam calls webhook with token
        //
        // Fields:
        //   - pageCode, userId, apiKey
        //   - action: "tokenize" or sum=0 with saveCardToken=1
        //   - successUrl, cancelUrl

        _logger.LogWarning("MeshulamGateway.TokenizePaymentMethodAsync called but not yet implemented");
        return new TokenizeResult(false, null, null, null, null, null, null, "Meshulam tokenization not configured.");
    }

    public async Task<ChargeResult> ChargeTokenAsync(ChargeTokenRequest req, CancellationToken ct = default)
    {
        // TODO: Implement with real Meshulam API
        // Endpoint: POST /chargeByToken
        // Fields:
        //   - userId, apiKey
        //   - cardToken: the saved token
        //   - sum: amount in agorot
        //   - description
        //
        // Response: { status: 1, data: { transactionId: "...", asmachta: "..." } }

        _logger.LogWarning("MeshulamGateway.ChargeTokenAsync called but not yet implemented");
        return new ChargeResult(false, null, "Meshulam charge-by-token not configured.");
    }

    public async Task<RefundResult> RefundAsync(RefundRequest req, CancellationToken ct = default)
    {
        // TODO: Implement with real Meshulam API
        // Endpoint: POST /refundTransaction
        // Fields: userId, apiKey, transactionId, sum (partial or full)

        _logger.LogWarning("MeshulamGateway.RefundAsync called but not yet implemented");
        return new RefundResult(false, null, "Meshulam refund not configured.");
    }

    public async Task<WebhookParseResult> ParseWebhookAsync(Stream body, IDictionary<string, string> headers, CancellationToken ct = default)
    {
        // TODO: Parse Meshulam webhook payload
        // Meshulam sends POST with form-encoded data:
        //   - customFields[cField1]: our internal reference
        //   - transactionId
        //   - asmachta (approval number)
        //   - cardSuffix (last 4)
        //   - cardBrand
        //   - status: 1 = success
        //   - processId
        //   - cardToken (if tokenization)

        using var reader = new StreamReader(body, Encoding.UTF8);
        var payload = await reader.ReadToEndAsync(ct);
        _logger.LogInformation("Meshulam webhook received, payload length: {Len}", payload.Length);

        // Placeholder: parse form-encoded data
        return new WebhookParseResult(
            Parsed: false,
            EventId: null,
            ProviderReference: null,
            Status: null,
            Token: null,
            Last4: null,
            Expiry: null,
            CardBrand: null,
            ProviderCustomerId: null,
            RawSignature: null,
            Error: "Meshulam webhook parsing not yet implemented");
    }

    public Task<bool> VerifyWebhookSignatureAsync(WebhookParseResult parsed, IDictionary<string, string> headers, CancellationToken ct = default)
    {
        // TODO: Meshulam uses a shared secret for webhook verification
        // Verify HMAC of the payload using WebhookSecretRef from Key Vault
        // FAIL-CLOSED: return false in production if not implemented
        _logger.LogWarning("Meshulam webhook signature verification not implemented — FAIL-CLOSED");
        return Task.FromResult(false);
    }
}
