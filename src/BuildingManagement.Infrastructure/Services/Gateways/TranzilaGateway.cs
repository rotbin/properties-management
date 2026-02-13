using System.Text;
using BuildingManagement.Core.Enums;
using BuildingManagement.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace BuildingManagement.Infrastructure.Services.Gateways;

/// <summary>
/// Tranzila (טרנזילה) payment gateway integration.
/// API Docs: https://docs.tranzila.com/
///
/// Tranzila supports:
///   - Hosted payment page (iframe mode)
///   - Tokenization (TrToken)
///   - Recurring charges via token
///   - Refunds (credit transactions)
///   - Webhook / IPN notifications
///
/// SECURITY: Supplier/terminal credentials must be in Azure Key Vault.
/// </summary>
public class TranzilaGateway : IPaymentGateway
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TranzilaGateway> _logger;

    // TODO: Set from PaymentProviderConfig.BaseUrl
    private const string BaseUrl = "https://secure5.tranzila.com";

    public TranzilaGateway(HttpClient httpClient, ILogger<TranzilaGateway> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public string ProviderName => "Tranzila (טרנזילה)";
    public PaymentProviderType ProviderType => PaymentProviderType.Tranzila;

    public async Task<PaymentSessionResult> CreatePaymentSessionAsync(CreatePaymentSessionRequest req, CancellationToken ct = default)
    {
        // TODO: Implement Tranzila hosted payment page
        // Tranzila uses an iframe approach:
        //   URL: https://direct.tranzila.com/{supplier}/iframe.php
        //   Parameters:
        //     - supplier: terminal name (from Key Vault via TerminalIdRef)
        //     - sum: amount (e.g., 500.00)
        //     - currency: 1 for ILS
        //     - cred_type: 1 (regular), 6 (token)
        //     - notify_url_address: webhook URL
        //     - success_url_address, fail_url_address
        //     - pdesc: description
        //     - contact: payer name
        //     - email: payer email
        //
        // The iframe URL is constructed — no API call needed for page creation.
        // Transaction ID is returned via webhook/redirect.

        _logger.LogWarning("TranzilaGateway.CreatePaymentSessionAsync not yet implemented with real credentials");
        return new PaymentSessionResult(false, null, null, null, "Tranzila gateway not configured. Add terminal credentials.");
    }

    public async Task<TokenizeResult> TokenizePaymentMethodAsync(TokenizeRequest req, CancellationToken ct = default)
    {
        // TODO: Implement Tranzila tokenization
        // Use iframe with TrToken=True parameter
        // On success, Tranzila returns:
        //   - TrToken: the card token
        //   - expdate: MMYY
        //   - cardtype: Visa/Mastercard/etc.
        //   - cardmask: ****1234

        _logger.LogWarning("TranzilaGateway.TokenizePaymentMethodAsync not yet implemented");
        return new TokenizeResult(false, null, null, null, null, null, null, "Tranzila tokenization not configured.");
    }

    public async Task<ChargeResult> ChargeTokenAsync(ChargeTokenRequest req, CancellationToken ct = default)
    {
        // TODO: Implement Tranzila charge by token
        // Endpoint: POST https://secure5.tranzila.com/cgi-bin/tranzila71u.cgi
        // Fields:
        //   - supplier: terminal (from Key Vault)
        //   - TranzilaTK: card token
        //   - sum: amount
        //   - currency: 1 (ILS)
        //   - cred_type: 1
        //   - tranmode: V (verify) or A (authorize+capture)
        //
        // Response (form-encoded):
        //   - Response: 000 = success
        //   - ConfirmationCode
        //   - index: transaction ID

        _logger.LogWarning("TranzilaGateway.ChargeTokenAsync not yet implemented");
        return new ChargeResult(false, null, "Tranzila charge-by-token not configured.");
    }

    public async Task<RefundResult> RefundAsync(RefundRequest req, CancellationToken ct = default)
    {
        // TODO: Implement Tranzila refund (credit transaction)
        // Same endpoint with CreditPass parameter and original transaction reference

        _logger.LogWarning("TranzilaGateway.RefundAsync not yet implemented");
        return new RefundResult(false, null, "Tranzila refund not configured.");
    }

    public async Task<WebhookParseResult> ParseWebhookAsync(Stream body, IDictionary<string, string> headers, CancellationToken ct = default)
    {
        // TODO: Parse Tranzila IPN notification
        // Tranzila sends POST to notify_url_address with form-encoded:
        //   - Response: 000 = success
        //   - ConfirmationCode
        //   - index (transaction ID)
        //   - TranzilaTK (if tokenization)
        //   - cardmask
        //   - cardtype
        //   - expdate

        using var reader = new StreamReader(body, Encoding.UTF8);
        var payload = await reader.ReadToEndAsync(ct);
        _logger.LogInformation("Tranzila webhook received, payload length: {Len}", payload.Length);

        return new WebhookParseResult(
            Parsed: false, EventId: null, ProviderReference: null, Status: null,
            Token: null, Last4: null, Expiry: null, CardBrand: null,
            ProviderCustomerId: null, RawSignature: null,
            Error: "Tranzila webhook parsing not yet implemented");
    }

    public Task<bool> VerifyWebhookSignatureAsync(WebhookParseResult parsed, IDictionary<string, string> headers, CancellationToken ct = default)
    {
        // TODO: Tranzila uses IP whitelisting + optional notify_url password
        // Verify source IP is from Tranzila's known ranges
        // FAIL-CLOSED in production
        _logger.LogWarning("Tranzila webhook signature verification not implemented — FAIL-CLOSED");
        return Task.FromResult(false);
    }
}
