using System.Text;
using BuildingManagement.Core.Enums;
using BuildingManagement.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace BuildingManagement.Infrastructure.Services.Gateways;

/// <summary>
/// Pelecard (פלאקרד) payment gateway integration.
/// API Docs: https://gateway20.pelecard.biz/
///
/// Pelecard supports:
///   - Hosted payment page (PelecardIframe / redirect)
///   - Tokenization (Token=True in init request)
///   - Recurring charges via J4/J5 (ChargeByToken)
///   - Refunds
///   - Webhook/IPN notifications
///
/// SECURITY: Terminal/User/Password must be in Azure Key Vault.
/// </summary>
public class PelecardGateway : IPaymentGateway
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PelecardGateway> _logger;

    // TODO: Set from PaymentProviderConfig.BaseUrl
    private const string SandboxBaseUrl = "https://gateway20.pelecard.biz";
    private const string ProductionBaseUrl = "https://gateway20.pelecard.biz";

    public PelecardGateway(HttpClient httpClient, ILogger<PelecardGateway> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public string ProviderName => "Pelecard (פלאקרד)";
    public PaymentProviderType ProviderType => PaymentProviderType.Pelecard;

    public async Task<PaymentSessionResult> CreatePaymentSessionAsync(CreatePaymentSessionRequest req, CancellationToken ct = default)
    {
        // TODO: Implement Pelecard Init API
        // Endpoint: POST /services/PaymentGW/init
        // Required JSON fields:
        //   - terminal: from Key Vault (TerminalIdRef)
        //   - user: from Key Vault (ApiUserRef)
        //   - password: from Key Vault (ApiPasswordRef)
        //   - GoodUrl: success redirect URL
        //   - ErrorUrl: cancel redirect URL
        //   - Total: amount in agorot (e.g., 50000 for 500 ILS)
        //   - Currency: 1 for ILS
        //   - ParamX: custom fields for reference
        //
        // Response: { URL: "https://gateway20.pelecard.biz/PaymentGW?transactionId=..." }

        _logger.LogWarning("PelecardGateway.CreatePaymentSessionAsync not yet implemented with real credentials");
        return new PaymentSessionResult(false, null, null, null, "Pelecard gateway not configured. Add terminal credentials.");
    }

    public async Task<TokenizeResult> TokenizePaymentMethodAsync(TokenizeRequest req, CancellationToken ct = default)
    {
        // TODO: Implement Pelecard Init with Token=True
        // Same as CreatePaymentSession but with Token=True flag
        // Pelecard returns the token in the redirect URL parameters or via IPN

        _logger.LogWarning("PelecardGateway.TokenizePaymentMethodAsync not yet implemented");
        return new TokenizeResult(false, null, null, null, null, null, null, "Pelecard tokenization not configured.");
    }

    public async Task<ChargeResult> ChargeTokenAsync(ChargeTokenRequest req, CancellationToken ct = default)
    {
        // TODO: Implement Pelecard J4/J5 charge by token
        // Endpoint: POST /services/PaymentGW/DebitRegularType
        // Fields:
        //   - terminal, user, password
        //   - Token: saved card token
        //   - Total: amount in agorot
        //   - Currency: 1 (ILS)
        //
        // Response: { PelecardTransactionId, ApprovalNo, ResultCode }

        _logger.LogWarning("PelecardGateway.ChargeTokenAsync not yet implemented");
        return new ChargeResult(false, null, "Pelecard charge-by-token not configured.");
    }

    public async Task<RefundResult> RefundAsync(RefundRequest req, CancellationToken ct = default)
    {
        // TODO: Implement Pelecard refund
        // Endpoint: POST /services/PaymentGW/Refund
        // Fields: terminal, user, password, PelecardTransactionId, Total

        _logger.LogWarning("PelecardGateway.RefundAsync not yet implemented");
        return new RefundResult(false, null, "Pelecard refund not configured.");
    }

    public async Task<WebhookParseResult> ParseWebhookAsync(Stream body, IDictionary<string, string> headers, CancellationToken ct = default)
    {
        // TODO: Parse Pelecard IPN / redirect parameters
        // Pelecard sends data as query parameters to GoodUrl/ErrorUrl:
        //   - PelecardTransactionId
        //   - PelecardStatusCode: "000" = success
        //   - ApprovalNo
        //   - Token (if tokenization)
        //   - ParamX: custom fields
        //
        // For webhook/IPN: parse form-encoded or JSON body

        using var reader = new StreamReader(body, Encoding.UTF8);
        var payload = await reader.ReadToEndAsync(ct);
        _logger.LogInformation("Pelecard webhook received, payload length: {Len}", payload.Length);

        return new WebhookParseResult(
            Parsed: false, EventId: null, ProviderReference: null, Status: null,
            Token: null, Last4: null, Expiry: null, CardBrand: null,
            ProviderCustomerId: null, RawSignature: null,
            Error: "Pelecard webhook parsing not yet implemented");
    }

    public Task<bool> VerifyWebhookSignatureAsync(WebhookParseResult parsed, IDictionary<string, string> headers, CancellationToken ct = default)
    {
        // TODO: Pelecard uses a ConfirmationKey mechanism
        // Endpoint: POST /services/PaymentGW/ValidateByUniqueKey with the transaction data
        // FAIL-CLOSED in production
        _logger.LogWarning("Pelecard webhook signature verification not implemented — FAIL-CLOSED");
        return Task.FromResult(false);
    }
}
