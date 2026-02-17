using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BuildingManagement.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BuildingManagement.Infrastructure.Services.Accounting;

/// <summary>
/// Israeli invoicing provider integration via Green Invoice REST API.
/// Docs: https://www.greeninvoice.co.il/api-docs
/// </summary>
public class GreenInvoiceProvider : IAccountingDocProvider
{
    private readonly HttpClient _http;
    private readonly ILogger<GreenInvoiceProvider> _logger;
    private readonly string _apiKey;
    private readonly string _apiSecret;
    private readonly string _baseUrl;

    private string? _accessToken;
    private DateTime _tokenExpiry;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    public string ProviderName => "GreenInvoice";

    public GreenInvoiceProvider(HttpClient http, IConfiguration config, ILogger<GreenInvoiceProvider> logger)
    {
        _http = http;
        _logger = logger;
        _apiKey = config["GreenInvoice:ApiKey"] ?? "";
        _apiSecret = config["GreenInvoice:ApiSecret"] ?? "";
        _baseUrl = config["GreenInvoice:BaseUrl"] ?? "https://api.greeninvoice.co.il/api/v1";
    }

    public async Task<AccountingDocResult> CreateReceiptAsync(CreateDocRequest request, CancellationToken ct = default)
    {
        // Green Invoice doc type 400 = Receipt (קבלה)
        return await CreateDocumentAsync(request, 400, ct);
    }

    public async Task<AccountingDocResult> CreateInvoiceAsync(CreateDocRequest request, CancellationToken ct = default)
    {
        // Green Invoice doc type 305 = Tax Invoice (חשבונית מס)
        return await CreateDocumentAsync(request, 305, ct);
    }

    private async Task<AccountingDocResult> CreateDocumentAsync(CreateDocRequest request, int docType, CancellationToken ct)
    {
        try
        {
            await EnsureTokenAsync(ct);

            var payload = new
            {
                type = docType,
                client = new
                {
                    name = request.Customer.Name,
                    emails = request.Customer.Email != null ? new[] { request.Customer.Email } : Array.Empty<string>(),
                    address = request.Customer.Address ?? "",
                    taxId = request.Customer.TaxId ?? ""
                },
                currency = request.Currency switch
                {
                    "ILS" => "ILS",
                    "USD" => "USD",
                    "EUR" => "EUR",
                    _ => "ILS"
                },
                date = request.Date.ToString("yyyy-MM-dd"),
                income = new[]
                {
                    new
                    {
                        description = request.Description,
                        quantity = 1,
                        price = request.Amount,
                        currency = request.Currency ?? "ILS",
                        vatType = 0
                    }
                },
                remarks = request.ExternalRef
            };

            var httpReq = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/documents")
            {
                Content = JsonContent.Create(payload)
            };
            httpReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);

            var response = await _http.SendAsync(httpReq, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Green Invoice create doc failed: {Status} {Body}", response.StatusCode, body);
                return new AccountingDocResult { Success = false, Error = $"Provider returned {response.StatusCode}" };
            }

            var doc = JsonSerializer.Deserialize<GreenInvoiceDocResponse>(body, JsonOpts);
            if (doc == null)
                return new AccountingDocResult { Success = false, Error = "Failed to parse provider response" };

            _logger.LogInformation("Green Invoice doc created: id={DocId} number={DocNumber} type={DocType}",
                doc.Id, doc.Number, docType);

            return new AccountingDocResult
            {
                Success = true,
                DocId = doc.Id,
                DocNumber = doc.Number?.ToString(),
                PdfUrl = doc.Url?.Pdf
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Green Invoice create doc failed for externalRef={ExternalRef}", request.ExternalRef);
            return new AccountingDocResult { Success = false, Error = ex.Message };
        }
    }

    private async Task EnsureTokenAsync(CancellationToken ct)
    {
        if (_accessToken != null && DateTime.UtcNow < _tokenExpiry)
            return;

        await _tokenLock.WaitAsync(ct);
        try
        {
            if (_accessToken != null && DateTime.UtcNow < _tokenExpiry)
                return;

            var response = await _http.PostAsJsonAsync($"{_baseUrl}/account/token", new
            {
                id = _apiKey,
                secret = _apiSecret
            }, ct);

            response.EnsureSuccessStatusCode();
            var token = await response.Content.ReadFromJsonAsync<GreenInvoiceTokenResponse>(JsonOpts, ct);
            _accessToken = token!.Token;
            _tokenExpiry = DateTime.UtcNow.AddMinutes(50); // Tokens last ~60 min
            _logger.LogInformation("Green Invoice token refreshed");
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private record GreenInvoiceTokenResponse(string Token);
    private record GreenInvoiceDocResponse(string Id, int? Number, GreenInvoiceDocUrl? Url);
    private record GreenInvoiceDocUrl(string? Pdf);
}
