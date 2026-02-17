using System.ComponentModel.DataAnnotations;

namespace BuildingManagement.Core.DTOs;

// ─── Manager Invoice DTOs ────────────────────────────────

public record ManagerInvoiceDto
{
    public int Id { get; init; }
    public string ManagerUserId { get; init; } = string.Empty;
    public int BuildingId { get; init; }
    public string? BuildingName { get; init; }
    public string Period { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string? InvoiceDocId { get; init; }
    public string? InvoiceDocNumber { get; init; }
    public string? InvoicePdfUrl { get; init; }
    public DateTime? IssuedAtUtc { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

public record IssueManagerInvoiceRequest
{
    [Required]
    public int BuildingId { get; init; }

    /// <summary>Period in YYYY-MM format.</summary>
    [Required, MaxLength(7)]
    public string Period { get; init; } = string.Empty;
}
