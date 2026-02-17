using BuildingManagement.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace BuildingManagement.Infrastructure.Services.Accounting;

/// <summary>
/// Fake accounting document provider for development and testing.
/// Generates fake doc IDs and a placeholder PDF URL.
/// </summary>
public class FakeAccountingDocProvider : IAccountingDocProvider
{
    private readonly ILogger<FakeAccountingDocProvider> _logger;
    private int _counter;

    public string ProviderName => "Fake";

    public FakeAccountingDocProvider(ILogger<FakeAccountingDocProvider> logger) => _logger = logger;

    public Task<AccountingDocResult> CreateReceiptAsync(CreateDocRequest request, CancellationToken ct = default)
    {
        var num = Interlocked.Increment(ref _counter);
        var docId = $"FAKE-RCP-{num:D6}";
        _logger.LogInformation("[FakeAccounting] Receipt issued: {DocId} for {ExternalRef}, amount={Amount}",
            docId, request.ExternalRef, request.Amount);

        return Task.FromResult(new AccountingDocResult
        {
            Success = true,
            DocId = docId,
            DocNumber = $"R-{num:D6}",
            PdfUrl = $"/fake-docs/receipt-{docId}.pdf"
        });
    }

    public Task<AccountingDocResult> CreateInvoiceAsync(CreateDocRequest request, CancellationToken ct = default)
    {
        var num = Interlocked.Increment(ref _counter);
        var docId = $"FAKE-INV-{num:D6}";
        _logger.LogInformation("[FakeAccounting] Invoice issued: {DocId} for {ExternalRef}, amount={Amount}",
            docId, request.ExternalRef, request.Amount);

        return Task.FromResult(new AccountingDocResult
        {
            Success = true,
            DocId = docId,
            DocNumber = $"I-{num:D6}",
            PdfUrl = $"/fake-docs/invoice-{docId}.pdf"
        });
    }
}
