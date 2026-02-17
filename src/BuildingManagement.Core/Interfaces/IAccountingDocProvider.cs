namespace BuildingManagement.Core.Interfaces;

/// <summary>
/// Pluggable interface for Israeli accounting document providers (e.g., Green Invoice, iCount).
/// Issues receipts (קבלות) and invoices (חשבוניות מס) via external APIs.
/// </summary>
public interface IAccountingDocProvider
{
    string ProviderName { get; }

    /// <summary>Issue a receipt (קבלה) for a payment.</summary>
    Task<AccountingDocResult> CreateReceiptAsync(CreateDocRequest request, CancellationToken ct = default);

    /// <summary>Issue a tax invoice (חשבונית מס) for services.</summary>
    Task<AccountingDocResult> CreateInvoiceAsync(CreateDocRequest request, CancellationToken ct = default);
}

public record CreateDocRequest
{
    /// <summary>Provider-specific issuer/business profile ID.</summary>
    public required string IssuerProfileId { get; init; }

    public required DocCustomer Customer { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required DateTime Date { get; init; }
    public required string Description { get; init; }

    /// <summary>Stable external reference for idempotency (e.g. "payment:123").</summary>
    public required string ExternalRef { get; init; }
}

public record DocCustomer
{
    public required string Name { get; init; }
    public string? Email { get; init; }
    public string? Address { get; init; }
    public string? TaxId { get; init; }
}

public record AccountingDocResult
{
    public bool Success { get; init; }
    public string? DocId { get; init; }
    public string? DocNumber { get; init; }
    public string? PdfUrl { get; init; }
    public string? Error { get; init; }
}
