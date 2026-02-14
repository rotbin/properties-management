using System.ComponentModel.DataAnnotations;
using BuildingManagement.Core.Enums;

namespace BuildingManagement.Core.DTOs;

public record VendorInvoiceDto
{
    public int Id { get; init; }
    public int BuildingId { get; init; }
    public string? BuildingName { get; init; }
    public int VendorId { get; init; }
    public string? VendorName { get; init; }
    public int? WorkOrderId { get; init; }
    public int? ServiceRequestId { get; init; }
    public string? Category { get; init; }
    public string? Description { get; init; }
    public string? InvoiceNumber { get; init; }
    public DateTime InvoiceDate { get; init; }
    public decimal Amount { get; init; }
    public decimal PaidAmount { get; init; }
    public decimal Balance { get; init; }
    public DateTime? DueDate { get; init; }
    public VendorInvoiceStatus Status { get; init; }
    public string? Notes { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

public record CreateVendorInvoiceRequest
{
    [Required]
    public int BuildingId { get; init; }
    [Required]
    public int VendorId { get; init; }
    public int? WorkOrderId { get; init; }
    public int? ServiceRequestId { get; init; }
    [MaxLength(100)]
    public string? Category { get; init; }
    [MaxLength(500)]
    public string? Description { get; init; }
    [MaxLength(100)]
    public string? InvoiceNumber { get; init; }
    [Required]
    public DateTime InvoiceDate { get; init; }
    [Required]
    public decimal Amount { get; init; }
    public DateTime? DueDate { get; init; }
    [MaxLength(1000)]
    public string? Notes { get; init; }
}

public record UpdateVendorInvoiceRequest
{
    public int VendorId { get; init; }
    public int? WorkOrderId { get; init; }
    public int? ServiceRequestId { get; init; }
    [MaxLength(100)]
    public string? Category { get; init; }
    [MaxLength(500)]
    public string? Description { get; init; }
    [MaxLength(100)]
    public string? InvoiceNumber { get; init; }
    public DateTime InvoiceDate { get; init; }
    public decimal Amount { get; init; }
    public DateTime? DueDate { get; init; }
    [MaxLength(1000)]
    public string? Notes { get; init; }
}

public record VendorPaymentDto
{
    public int Id { get; init; }
    public int VendorInvoiceId { get; init; }
    public decimal PaidAmount { get; init; }
    public DateTime PaidAtUtc { get; init; }
    public VendorPaymentMethod PaymentMethod { get; init; }
    public string? Reference { get; init; }
    public string? Notes { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

public record CreateVendorPaymentRequest
{
    [Required]
    public decimal PaidAmount { get; init; }
    public DateTime? PaidAtUtc { get; init; }
    public VendorPaymentMethod PaymentMethod { get; init; }
    [MaxLength(200)]
    public string? Reference { get; init; }
    [MaxLength(500)]
    public string? Notes { get; init; }
}
