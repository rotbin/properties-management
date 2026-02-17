using BuildingManagement.Core.DTOs;
using BuildingManagement.Core.Entities;
using BuildingManagement.Core.Entities.Finance;
using BuildingManagement.Core.Enums;
using BuildingManagement.Core.Interfaces;
using BuildingManagement.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BuildingManagement.Api.Controllers;

[ApiController]
[Route("api/accounting")]
[Authorize]
public class AccountingDocsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAccountingDocProvider _docProvider;
    private readonly ILogger<AccountingDocsController> _logger;

    public AccountingDocsController(AppDbContext db, IAccountingDocProvider docProvider, ILogger<AccountingDocsController> logger)
    {
        _db = db;
        _docProvider = docProvider;
        _logger = logger;
    }

    // ═══════════════════════════════════════════════════════
    // TENANT: Payments with receipt info
    // ═══════════════════════════════════════════════════════

    /// <summary>Get tenant's payments with receipt download info.</summary>
    [HttpGet("my-payments")]
    [Authorize(Roles = AppRoles.Tenant)]
    public async Task<ActionResult<List<TenantPaymentDto>>> GetMyPayments()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;

        var payments = await _db.Payments
            .Include(p => p.Unit).ThenInclude(u => u.Building)
            .Include(p => p.Allocations).ThenInclude(a => a.UnitCharge)
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.PaymentDateUtc)
            .ToListAsync();

        return Ok(payments.Select(p => new TenantPaymentDto
        {
            Id = p.Id,
            Amount = p.Amount,
            PaymentDateUtc = p.PaymentDateUtc,
            Status = p.Status.ToString(),
            BuildingName = p.Unit?.Building?.Name,
            UnitNumber = p.Unit?.UnitNumber,
            Period = p.Allocations.FirstOrDefault()?.UnitCharge?.Period,
            ReceiptDocNumber = p.ReceiptDocNumber,
            ReceiptPdfUrl = p.ReceiptPdfUrl,
            ReceiptIssuedAtUtc = p.ReceiptIssuedAtUtc,
            HasReceipt = p.ReceiptDocId != null
        }).ToList());
    }

    /// <summary>Get receipt PDF URL for a specific payment. Tenant can only access their own.</summary>
    [HttpGet("my-payments/{paymentId}/receipt")]
    [Authorize(Roles = AppRoles.Tenant)]
    public async Task<IActionResult> GetReceipt(int paymentId)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
        var payment = await _db.Payments.FirstOrDefaultAsync(p => p.Id == paymentId && p.UserId == userId);
        if (payment == null) return NotFound();
        if (payment.ReceiptPdfUrl == null) return NotFound(new { message = "Receipt not yet issued." });

        return Ok(new { pdfUrl = payment.ReceiptPdfUrl, docNumber = payment.ReceiptDocNumber });
    }

    /// <summary>
    /// Issue a receipt for a paid payment. Idempotent: returns existing receipt if already issued.
    /// Called automatically from payment webhook or can be retried manually.
    /// </summary>
    [HttpPost("payments/{paymentId}/issue-receipt")]
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<IActionResult> IssueReceipt(int paymentId)
    {
        return await IssueReceiptForPayment(paymentId);
    }

    // ═══════════════════════════════════════════════════════
    // MANAGER: Invoices
    // ═══════════════════════════════════════════════════════

    /// <summary>List manager's invoices, optionally filtered by period.</summary>
    [HttpGet("invoices")]
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<ActionResult<List<ManagerInvoiceDto>>> GetInvoices([FromQuery] string? period)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
        var isAdmin = User.IsInRole(AppRoles.Admin);

        IQueryable<ManagerInvoice> query = _db.ManagerInvoices
            .Include(mi => mi.Building);

        if (!isAdmin) query = query.Where(mi => mi.ManagerUserId == userId);
        if (!string.IsNullOrEmpty(period)) query = query.Where(mi => mi.Period == period);

        var items = await query.OrderByDescending(mi => mi.Period).ThenBy(mi => mi.BuildingId).ToListAsync();

        return Ok(items.Select(MapInvoiceDto).ToList());
    }

    /// <summary>
    /// Issue an invoice for manager services to building committee. Idempotent by (manager, building, period).
    /// </summary>
    [HttpPost("invoices")]
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<ActionResult<ManagerInvoiceDto>> IssueInvoice([FromBody] IssueManagerInvoiceRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return Unauthorized();

        // Check issuer profile
        if (string.IsNullOrEmpty(user.IssuerProfileId))
            return BadRequest(new { message = "Manager issuer profile is not configured. Set your IssuerProfileId first." });

        // Check building access
        var isAdmin = User.IsInRole(AppRoles.Admin);
        if (!isAdmin)
        {
            var hasAccess = await _db.BuildingManagers.AnyAsync(bm => bm.UserId == userId && bm.BuildingId == request.BuildingId);
            if (!hasAccess) return Forbid();
        }

        var building = await _db.Buildings.FindAsync(request.BuildingId);
        if (building == null) return BadRequest(new { message = "Building not found." });

        // Idempotent: return existing if already issued
        var existing = await _db.ManagerInvoices.FirstOrDefaultAsync(mi =>
            mi.ManagerUserId == userId && mi.BuildingId == request.BuildingId && mi.Period == request.Period);
        if (existing != null)
        {
            await _db.Entry(existing).Reference(e => e.Building).LoadAsync();
            return Ok(MapInvoiceDto(existing));
        }

        // Determine fee amount from HOA fee plan or use a default
        var fee = await GetManagerFee(userId, request.BuildingId);
        if (fee <= 0)
            return BadRequest(new { message = "No manager service fee configured for this building." });

        var customerName = building.CommitteeLegalName ?? $"ועד הבית - {building.Name}";
        var customerAddress = $"{building.AddressLine}, {building.City}".Trim().TrimEnd(',');

        var externalRef = $"mgrinvoice:{userId}:{request.BuildingId}:{request.Period}";

        var result = await _docProvider.CreateInvoiceAsync(new CreateDocRequest
        {
            IssuerProfileId = user.IssuerProfileId,
            Customer = new DocCustomer { Name = customerName, Address = customerAddress },
            Amount = fee,
            Currency = "ILS",
            Date = DateTime.UtcNow,
            Description = $"Building management services – {building.Name} – {request.Period}",
            ExternalRef = externalRef
        });

        if (!result.Success)
            return StatusCode(502, new { message = $"Invoicing provider error: {result.Error}" });

        var invoice = new ManagerInvoice
        {
            ManagerUserId = userId,
            BuildingId = request.BuildingId,
            Period = request.Period,
            Amount = fee,
            InvoiceDocId = result.DocId,
            InvoiceDocNumber = result.DocNumber,
            InvoicePdfUrl = result.PdfUrl,
            IssuedAtUtc = DateTime.UtcNow
        };

        _db.ManagerInvoices.Add(invoice);
        await _db.SaveChangesAsync();

        invoice.Building = building;
        return CreatedAtAction(nameof(GetInvoicePdf), new { id = invoice.Id }, MapInvoiceDto(invoice));
    }

    /// <summary>Get invoice PDF URL.</summary>
    [HttpGet("invoices/{id}/pdf")]
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<IActionResult> GetInvoicePdf(int id)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
        var isAdmin = User.IsInRole(AppRoles.Admin);

        var invoice = await _db.ManagerInvoices.FindAsync(id);
        if (invoice == null) return NotFound();
        if (!isAdmin && invoice.ManagerUserId != userId) return Forbid();
        if (invoice.InvoicePdfUrl == null) return NotFound(new { message = "Invoice PDF not available." });

        return Ok(new { pdfUrl = invoice.InvoicePdfUrl, docNumber = invoice.InvoiceDocNumber });
    }

    // ═══════════════════════════════════════════════════════
    // Internal: receipt issuance (called from webhook or API)
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Issues a receipt for a payment. Idempotent: if receipt already exists, does nothing.
    /// Uses "update where ReceiptDocId is null" pattern to prevent duplicates.
    /// </summary>
    public async Task<IActionResult> IssueReceiptForPayment(int paymentId)
    {
        var payment = await _db.Payments
            .Include(p => p.Unit).ThenInclude(u => u.Building)
            .Include(p => p.User)
            .Include(p => p.Allocations).ThenInclude(a => a.UnitCharge)
            .FirstOrDefaultAsync(p => p.Id == paymentId);

        if (payment == null)
            return NotFound(new { message = "Payment not found." });

        if (payment.Status != PaymentStatus.Succeeded)
            return BadRequest(new { message = "Payment is not in Succeeded status." });

        // Already issued – idempotent return
        if (payment.ReceiptDocId != null)
            return Ok(new { docId = payment.ReceiptDocId, docNumber = payment.ReceiptDocNumber, pdfUrl = payment.ReceiptPdfUrl });

        var building = payment.Unit?.Building;
        if (building == null)
            return BadRequest(new { message = "Building not found for this payment." });

        if (string.IsNullOrEmpty(building.IssuerProfileId))
            return BadRequest(new { message = "Building issuer profile is not configured." });

        var period = payment.Allocations.FirstOrDefault()?.UnitCharge?.Period ?? "N/A";
        var tenantName = payment.User?.FullName ?? "Tenant";
        var tenantEmail = payment.User?.Email;

        var externalRef = $"payment:{payment.Id}";

        var result = await _docProvider.CreateReceiptAsync(new CreateDocRequest
        {
            IssuerProfileId = building.IssuerProfileId,
            Customer = new DocCustomer { Name = tenantName, Email = tenantEmail },
            Amount = payment.Amount,
            Currency = "ILS",
            Date = payment.PaymentDateUtc,
            Description = $"HOA payment – {building.Name} – {period}",
            ExternalRef = externalRef
        });

        if (!result.Success)
        {
            _logger.LogWarning("Receipt issuance failed for payment {PaymentId}: {Error}", paymentId, result.Error);
            return StatusCode(502, new { message = $"Receipt provider error: {result.Error}" });
        }

        // Atomic update: only set if ReceiptDocId is still null (prevents race conditions)
        var updated = await _db.Payments
            .Where(p => p.Id == paymentId && p.ReceiptDocId == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.ReceiptDocId, result.DocId)
                .SetProperty(p => p.ReceiptDocNumber, result.DocNumber)
                .SetProperty(p => p.ReceiptPdfUrl, result.PdfUrl)
                .SetProperty(p => p.ReceiptIssuedAtUtc, DateTime.UtcNow));

        if (updated == 0)
        {
            _logger.LogInformation("Receipt already issued for payment {PaymentId} (concurrent request)", paymentId);
            var existing = await _db.Payments.AsNoTracking().FirstAsync(p => p.Id == paymentId);
            return Ok(new { docId = existing.ReceiptDocId, docNumber = existing.ReceiptDocNumber, pdfUrl = existing.ReceiptPdfUrl });
        }

        _logger.LogInformation("Receipt issued for payment {PaymentId}: doc={DocId} number={DocNumber}",
            paymentId, result.DocId, result.DocNumber);

        return Ok(new { docId = result.DocId, docNumber = result.DocNumber, pdfUrl = result.PdfUrl });
    }

    // ═══════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════

    private async Task<decimal> GetManagerFee(string managerId, int buildingId)
    {
        // Check if there's a configured fee plan; for now use a simple fixed amount
        // In the future this could come from a ManagerBuildingFee table
        var plan = await _db.HOAFeePlans
            .Where(h => h.BuildingId == buildingId && h.IsActive && !h.IsDeleted)
            .FirstOrDefaultAsync();

        // Use fixed per unit as a proxy for manager fee, or default
        return plan?.FixedAmountPerUnit ?? 0;
    }

    private static ManagerInvoiceDto MapInvoiceDto(ManagerInvoice mi) => new()
    {
        Id = mi.Id,
        ManagerUserId = mi.ManagerUserId,
        BuildingId = mi.BuildingId,
        BuildingName = mi.Building?.Name,
        Period = mi.Period,
        Amount = mi.Amount,
        InvoiceDocId = mi.InvoiceDocId,
        InvoiceDocNumber = mi.InvoiceDocNumber,
        InvoicePdfUrl = mi.InvoicePdfUrl,
        IssuedAtUtc = mi.IssuedAtUtc,
        CreatedAtUtc = mi.CreatedAtUtc
    };
}

// ─── Tenant Payment DTO (includes receipt info) ─────────

public record TenantPaymentDto
{
    public int Id { get; init; }
    public decimal Amount { get; init; }
    public DateTime PaymentDateUtc { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? BuildingName { get; init; }
    public string? UnitNumber { get; init; }
    public string? Period { get; init; }
    public string? ReceiptDocNumber { get; init; }
    public string? ReceiptPdfUrl { get; init; }
    public DateTime? ReceiptIssuedAtUtc { get; init; }
    public bool HasReceipt { get; init; }
}
