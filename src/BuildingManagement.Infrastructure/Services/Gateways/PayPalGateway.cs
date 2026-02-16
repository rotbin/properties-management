using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BuildingManagement.Core.Enums;
using BuildingManagement.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace BuildingManagement.Infrastructure.Services.Gateways;

/// <summary>
/// PayPal payment gateway integration using REST API v2.
/// API Docs: https://developer.paypal.com/docs/api/
///
/// PayPal supports:
///   - Hosted payment page (Orders API — checkout flow)
///   - Tokenization (vault / billing agreements)
///   - Subscriptions (standing orders via Subscriptions API)
///   - Refunds
///   - Webhook notifications with signature verification
///
/// SECURITY: Client ID and Secret must be stored in Azure Key Vault.
/// MerchantIdRef -> Key Vault secret for PayPal Client ID
/// ApiPasswordRef -> Key Vault secret for PayPal Client Secret
/// WebhookSecretRef -> Key Vault secret for PayPal Webhook ID
/// </summary>
public class PayPalGateway : IPaymentGateway
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PayPalGateway> _logger;

    private const string SandboxBaseUrl = "https://api-m.sandbox.paypal.com";
    private const string ProductionBaseUrl = "https://api-m.paypal.com";

    private string? _cachedAccessToken;
    private DateTime _tokenExpiry = DateTime.MinValue;

    public PayPalGateway(HttpClient httpClient, ILogger<PayPalGateway> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public string ProviderName => "PayPal";
    public PaymentProviderType ProviderType => PaymentProviderType.PayPal;

    /// <summary>
    /// Get OAuth 2.0 access token from PayPal.
    /// In a real implementation, clientId and clientSecret would be resolved from Key Vault.
    /// </summary>
    private async Task<string?> GetAccessTokenAsync(string? clientId, string? clientSecret, string baseUrl, CancellationToken ct)
    {
        if (_cachedAccessToken != null && DateTime.UtcNow < _tokenExpiry)
            return _cachedAccessToken;

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
        {
            _logger.LogWarning("PayPal credentials not configured");
            return null;
        }

        var authValue = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{clientId}:{clientSecret}"));
        var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/v1/oauth2/token");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authValue);
        request.Content = new StringContent("grant_type=client_credentials", Encoding.UTF8, "application/x-www-form-urlencoded");

        var response = await _httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("PayPal OAuth token request failed: {Status}", response.StatusCode);
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        var tokenResponse = JsonSerializer.Deserialize<PayPalTokenResponse>(json);
        if (tokenResponse?.AccessToken == null) return null;

        _cachedAccessToken = tokenResponse.AccessToken;
        _tokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn - 60);
        return _cachedAccessToken;
    }

    public async Task<PaymentSessionResult> CreatePaymentSessionAsync(CreatePaymentSessionRequest req, CancellationToken ct = default)
    {
        // TODO: Resolve clientId/clientSecret from Key Vault via PaymentProviderConfig
        // For now, log warning and return not-configured error
        _logger.LogWarning("PayPalGateway.CreatePaymentSessionAsync — implement with real PayPal credentials");

        // PayPal Orders API v2:
        // POST /v2/checkout/orders
        // {
        //   "intent": "CAPTURE",
        //   "purchase_units": [{
        //     "reference_id": "{unitChargeId}",
        //     "amount": { "currency_code": "ILS", "value": "500.00" },
        //     "description": "HOA Payment - Unit 101 - 2026-02"
        //   }],
        //   "application_context": {
        //     "return_url": "{successUrl}",
        //     "cancel_url": "{cancelUrl}",
        //     "brand_name": "HomeHero",
        //     "landing_page": "BILLING",
        //     "user_action": "PAY_NOW"
        //   }
        // }
        //
        // Response contains: { "id": "ORDER_ID", "links": [{ "rel": "approve", "href": "https://..." }] }
        // The "approve" link is the PaymentUrl the tenant redirects to.

        return new PaymentSessionResult(
            false, null, null, null,
            "PayPal gateway not configured. Add Client ID and Secret in provider settings.");
    }

    public async Task<TokenizeResult> TokenizePaymentMethodAsync(TokenizeRequest req, CancellationToken ct = default)
    {
        // PayPal Vault API:
        // POST /v3/vault/setup-tokens
        // Creates a setup token that lets the customer save their PayPal account
        // for future payments.
        //
        // After customer approval, use POST /v3/vault/payment-tokens
        // to create a permanent token from the setup token.

        _logger.LogWarning("PayPalGateway.TokenizePaymentMethodAsync — not yet implemented");
        return new TokenizeResult(false, null, null, null, null, null, null,
            "PayPal tokenization not configured. Add credentials in provider settings.");
    }

    public async Task<ChargeResult> ChargeTokenAsync(ChargeTokenRequest req, CancellationToken ct = default)
    {
        // PayPal: Charge a vaulted payment source
        // POST /v2/checkout/orders (with vault token as payment_source)
        // Then POST /v2/checkout/orders/{id}/capture

        _logger.LogWarning("PayPalGateway.ChargeTokenAsync — not yet implemented");
        return new ChargeResult(false, null, "PayPal charge-by-token not configured.");
    }

    public async Task<RefundResult> RefundAsync(RefundRequest req, CancellationToken ct = default)
    {
        // PayPal Refund:
        // POST /v2/payments/captures/{capture_id}/refund
        // { "amount": { "value": "100.00", "currency_code": "ILS" }, "note_to_payer": "Refund reason" }

        _logger.LogWarning("PayPalGateway.RefundAsync — not yet implemented");
        return new RefundResult(false, null, "PayPal refund not configured.");
    }

    public async Task<WebhookParseResult> ParseWebhookAsync(Stream body, IDictionary<string, string> headers, CancellationToken ct = default)
    {
        using var reader = new StreamReader(body, Encoding.UTF8);
        var payload = await reader.ReadToEndAsync(ct);
        _logger.LogInformation("PayPal webhook received, payload length: {Len}", payload.Length);

        try
        {
            var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            var eventType = root.TryGetProperty("event_type", out var et) ? et.GetString() : null;
            var eventId = root.TryGetProperty("id", out var eid) ? eid.GetString() : null;

            // Handle different PayPal webhook event types
            string? providerReference = null;
            PaymentStatus? status = null;
            string? subscriptionId = null;

            switch (eventType)
            {
                case "CHECKOUT.ORDER.APPROVED":
                case "PAYMENT.CAPTURE.COMPLETED":
                    status = PaymentStatus.Succeeded;
                    if (root.TryGetProperty("resource", out var resource))
                    {
                        providerReference = resource.TryGetProperty("id", out var rid) ? rid.GetString() : null;
                    }
                    break;

                case "PAYMENT.CAPTURE.DENIED":
                case "PAYMENT.CAPTURE.DECLINED":
                    status = PaymentStatus.Failed;
                    if (root.TryGetProperty("resource", out var failResource))
                    {
                        providerReference = failResource.TryGetProperty("id", out var frid) ? frid.GetString() : null;
                    }
                    break;

                case "PAYMENT.CAPTURE.REFUNDED":
                    status = PaymentStatus.Refunded;
                    if (root.TryGetProperty("resource", out var refundResource))
                    {
                        providerReference = refundResource.TryGetProperty("id", out var rrid) ? rrid.GetString() : null;
                    }
                    break;

                case "BILLING.SUBSCRIPTION.ACTIVATED":
                case "BILLING.SUBSCRIPTION.UPDATED":
                case "BILLING.SUBSCRIPTION.CANCELLED":
                case "BILLING.SUBSCRIPTION.SUSPENDED":
                case "BILLING.SUBSCRIPTION.PAYMENT.FAILED":
                    // Standing order events handled by the controller
                    if (root.TryGetProperty("resource", out var subResource))
                    {
                        subscriptionId = subResource.TryGetProperty("id", out var sid) ? sid.GetString() : null;
                        providerReference = subscriptionId;
                    }
                    break;

                default:
                    _logger.LogInformation("Unhandled PayPal event type: {EventType}", eventType);
                    break;
            }

            return new WebhookParseResult(
                Parsed: true,
                EventId: eventId,
                ProviderReference: providerReference,
                Status: status,
                Token: subscriptionId,
                Last4: null,
                Expiry: null,
                CardBrand: null,
                ProviderCustomerId: null,
                RawSignature: headers.TryGetValue("paypal-transmission-sig", out var sig) ? sig : null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse PayPal webhook");
            return new WebhookParseResult(
                Parsed: false, EventId: null, ProviderReference: null, Status: null,
                Token: null, Last4: null, Expiry: null, CardBrand: null,
                ProviderCustomerId: null, RawSignature: null,
                Error: $"Failed to parse PayPal webhook: {ex.Message}");
        }
    }

    public async Task<bool> VerifyWebhookSignatureAsync(WebhookParseResult parsed, IDictionary<string, string> headers, CancellationToken ct = default)
    {
        // PayPal webhook signature verification:
        // POST /v1/notifications/verify-webhook-signature
        // {
        //   "auth_algo": headers["paypal-auth-algo"],
        //   "cert_url": headers["paypal-cert-url"],
        //   "transmission_id": headers["paypal-transmission-id"],
        //   "transmission_sig": headers["paypal-transmission-sig"],
        //   "transmission_time": headers["paypal-transmission-time"],
        //   "webhook_id": "{from WebhookSecretRef in Key Vault}",
        //   "webhook_event": {original JSON body}
        // }
        //
        // Response: { "verification_status": "SUCCESS" }

        _logger.LogWarning("PayPal webhook signature verification not implemented — FAIL-CLOSED");
        return false;
    }

    // ─── PayPal Subscription (Standing Order) Methods ────────

    /// <summary>
    /// Create a PayPal subscription plan for recurring HOA payments.
    /// POST /v1/billing/plans
    /// </summary>
    public async Task<PayPalPlanResult> CreateBillingPlanAsync(
        string productId, decimal amount, string currency, string description,
        string? clientId, string? clientSecret, string? baseUrl, CancellationToken ct = default)
    {
        var apiBase = baseUrl ?? SandboxBaseUrl;
        var token = await GetAccessTokenAsync(clientId, clientSecret, apiBase, ct);
        if (token == null)
            return new PayPalPlanResult(false, null, "Failed to obtain PayPal access token");

        var planBody = new
        {
            product_id = productId,
            name = description,
            description = $"Monthly HOA payment - {description}",
            status = "ACTIVE",
            billing_cycles = new[]
            {
                new
                {
                    frequency = new { interval_unit = "MONTH", interval_count = 1 },
                    tenure_type = "REGULAR",
                    sequence = 1,
                    total_cycles = 0,
                    pricing_scheme = new
                    {
                        fixed_price = new { value = amount.ToString("F2"), currency_code = currency }
                    }
                }
            },
            payment_preferences = new
            {
                auto_bill_outstanding = true,
                payment_failure_threshold = 3
            }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, $"{apiBase}/v1/billing/plans");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = new StringContent(JsonSerializer.Serialize(planBody), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("PayPal create plan failed: {Status} {Body}", response.StatusCode, errorBody);
            return new PayPalPlanResult(false, null, $"PayPal API error: {response.StatusCode}");
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        var planResponse = JsonSerializer.Deserialize<PayPalPlanResponse>(json);
        return new PayPalPlanResult(true, planResponse?.Id, null);
    }

    /// <summary>
    /// Create a PayPal subscription for a tenant.
    /// POST /v1/billing/subscriptions
    /// Returns an approval URL the tenant must visit to approve the subscription.
    /// </summary>
    public async Task<PayPalSubscriptionResult> CreateSubscriptionAsync(
        string planId, string returnUrl, string cancelUrl,
        string? clientId, string? clientSecret, string? baseUrl, CancellationToken ct = default)
    {
        var apiBase = baseUrl ?? SandboxBaseUrl;
        var token = await GetAccessTokenAsync(clientId, clientSecret, apiBase, ct);
        if (token == null)
            return new PayPalSubscriptionResult(false, null, null, "Failed to obtain PayPal access token");

        var subBody = new
        {
            plan_id = planId,
            application_context = new
            {
                brand_name = "HomeHero",
                locale = "en-US",
                shipping_preference = "NO_SHIPPING",
                user_action = "SUBSCRIBE_NOW",
                return_url = returnUrl,
                cancel_url = cancelUrl
            }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, $"{apiBase}/v1/billing/subscriptions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = new StringContent(JsonSerializer.Serialize(subBody), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("PayPal create subscription failed: {Status} {Body}", response.StatusCode, errorBody);
            return new PayPalSubscriptionResult(false, null, null, $"PayPal API error: {response.StatusCode}");
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        var subResponse = JsonSerializer.Deserialize<PayPalSubscriptionResponse>(json);

        var approveLink = subResponse?.Links?.FirstOrDefault(l => l.Rel == "approve")?.Href;
        return new PayPalSubscriptionResult(true, subResponse?.Id, approveLink, null);
    }

    /// <summary>
    /// Cancel a PayPal subscription.
    /// POST /v1/billing/subscriptions/{id}/cancel
    /// </summary>
    public async Task<bool> CancelSubscriptionAsync(
        string subscriptionId, string reason,
        string? clientId, string? clientSecret, string? baseUrl, CancellationToken ct = default)
    {
        var apiBase = baseUrl ?? SandboxBaseUrl;
        var token = await GetAccessTokenAsync(clientId, clientSecret, apiBase, ct);
        if (token == null) return false;

        var request = new HttpRequestMessage(HttpMethod.Post, $"{apiBase}/v1/billing/subscriptions/{subscriptionId}/cancel");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = new StringContent(
            JsonSerializer.Serialize(new { reason }),
            Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request, ct);
        return response.IsSuccessStatusCode;
    }
}

// ─── PayPal DTO Records ─────────────────────────────────

public record PayPalTokenResponse(
    [property: JsonPropertyName("access_token")] string? AccessToken,
    [property: JsonPropertyName("token_type")] string? TokenType,
    [property: JsonPropertyName("expires_in")] int ExpiresIn);

public record PayPalPlanResponse(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("status")] string? Status);

public record PayPalSubscriptionResponse(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("links")] List<PayPalLink>? Links);

public record PayPalLink(
    [property: JsonPropertyName("href")] string? Href,
    [property: JsonPropertyName("rel")] string? Rel);

public record PayPalPlanResult(bool Success, string? PlanId, string? Error);
public record PayPalSubscriptionResult(bool Success, string? SubscriptionId, string? ApprovalUrl, string? Error);
