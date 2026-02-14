using BuildingManagement.Core.DTOs;
using BuildingManagement.Core.Entities.Finance;
using BuildingManagement.Core.Enums;
using BuildingManagement.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BuildingManagement.Api.Controllers;

[ApiController]
[Route("api/vendor-invoices")]
[Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
public class VendorInvoicesController : ControllerBase
{
    private readonly AppDbContext _db;
    public VendorInvoicesController(AppDbContext db) => _db = db;

    // ─── LIST ────────────────────────────────────────────

    [HttpGet]
    public async Task<ActionResult<List<VendorInvoiceDto>>> GetAll(
        [FromQuery] int? buildingId, [FromQuery] int? vendorId,
        [FromQuery] VendorInvoiceStatus? status,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var query = _db.VendorInvoices
            .Include(vi => vi.Building)
            .Include(vi => vi.Vendor)
            .Include(vi => vi.Payments)
            .AsQueryable();

        if (buildingId.HasValue) query = query.Where(vi => vi.BuildingId == buildingId);
        if (vendorId.HasValue) query = query.Where(vi => vi.VendorId == vendorId);
        if (status.HasValue) query = query.Where(vi => vi.Status == status);
        if (from.HasValue) query = query.Where(vi => vi.InvoiceDate >= from);
        if (to.HasValue) query = query.Where(vi => vi.InvoiceDate <= to);

        var items = await query.OrderByDescending(vi => vi.InvoiceDate).ToListAsync();
        return Ok(items.Select(MapDto).ToList());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<VendorInvoiceDto>> GetById(int id)
    {
        var vi = await _db.VendorInvoices
            .Include(v => v.Building).Include(v => v.Vendor).Include(v => v.Payments)
            .FirstOrDefaultAsync(v => v.Id == id);
        if (vi == null) return NotFound();
        return Ok(MapDto(vi));
    }

    // ─── CREATE ──────────────────────────────────────────

    [HttpPost]
    public async Task<ActionResult<VendorInvoiceDto>> Create([FromBody] CreateVendorInvoiceRequest request)
    {
        var invoice = new VendorInvoice
        {
            BuildingId = request.BuildingId,
            VendorId = request.VendorId,
            WorkOrderId = request.WorkOrderId,
            ServiceRequestId = request.ServiceRequestId,
            Category = request.Category,
            Description = request.Description,
            InvoiceNumber = request.InvoiceNumber,
            InvoiceDate = request.InvoiceDate,
            Amount = request.Amount,
            DueDate = request.DueDate,
            Notes = request.Notes,
            Status = VendorInvoiceStatus.Draft,
            CreatedBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        };

        _db.VendorInvoices.Add(invoice);
        await _db.SaveChangesAsync();

        var created = await _db.VendorInvoices
            .Include(v => v.Building).Include(v => v.Vendor).Include(v => v.Payments)
            .FirstAsync(v => v.Id == invoice.Id);
        return Ok(MapDto(created));
    }

    // ─── UPDATE ──────────────────────────────────────────

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateVendorInvoiceRequest request)
    {
        var invoice = await _db.VendorInvoices.FindAsync(id);
        if (invoice == null) return NotFound();

        invoice.VendorId = request.VendorId;
        invoice.WorkOrderId = request.WorkOrderId;
        invoice.ServiceRequestId = request.ServiceRequestId;
        invoice.Category = request.Category;
        invoice.Description = request.Description;
        invoice.InvoiceNumber = request.InvoiceNumber;
        invoice.InvoiceDate = request.InvoiceDate;
        invoice.Amount = request.Amount;
        invoice.DueDate = request.DueDate;
        invoice.Notes = request.Notes;
        invoice.UpdatedBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ─── APPROVE ─────────────────────────────────────────

    [HttpPost("{id}/approve")]
    public async Task<IActionResult> Approve(int id)
    {
        var invoice = await _db.VendorInvoices.Include(v => v.Building).FirstOrDefaultAsync(v => v.Id == id);
        if (invoice == null) return NotFound();
        if (invoice.Status != VendorInvoiceStatus.Draft)
            return BadRequest(new { message = "Only Draft invoices can be approved." });

        invoice.Status = VendorInvoiceStatus.Approved;
        invoice.UpdatedBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        // Create expense ledger entry
        _db.LedgerEntries.Add(new LedgerEntry
        {
            BuildingId = invoice.BuildingId,
            EntryType = LedgerEntryType.Expense,
            Category = invoice.Category ?? "Other",
            Description = $"Vendor invoice #{invoice.InvoiceNumber ?? invoice.Id.ToString()}: {invoice.Description}",
            ReferenceId = invoice.Id,
            Debit = invoice.Amount,
            Credit = 0,
            BalanceAfter = 0, // simplified
            CreatedAtUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ─── CANCEL ──────────────────────────────────────────

    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> Cancel(int id)
    {
        var invoice = await _db.VendorInvoices.FindAsync(id);
        if (invoice == null) return NotFound();
        invoice.Status = VendorInvoiceStatus.Cancelled;
        invoice.UpdatedBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ─── DELETE (soft) ───────────────────────────────────

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var invoice = await _db.VendorInvoices.FindAsync(id);
        if (invoice == null) return NotFound();
        invoice.IsDeleted = true;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ─── PAYMENTS ────────────────────────────────────────

    [HttpGet("{id}/payments")]
    public async Task<ActionResult<List<VendorPaymentDto>>> GetPayments(int id)
    {
        var payments = await _db.VendorPayments
            .Where(vp => vp.VendorInvoiceId == id)
            .OrderByDescending(vp => vp.PaidAtUtc)
            .ToListAsync();
        return Ok(payments.Select(MapPaymentDto).ToList());
    }

    [HttpPost("{id}/payments")]
    public async Task<ActionResult<VendorPaymentDto>> AddPayment(int id, [FromBody] CreateVendorPaymentRequest request)
    {
        var invoice = await _db.VendorInvoices.Include(v => v.Payments).FirstOrDefaultAsync(v => v.Id == id);
        if (invoice == null) return NotFound();

        var payment = new VendorPayment
        {
            VendorInvoiceId = id,
            PaidAmount = request.PaidAmount,
            PaidAtUtc = request.PaidAtUtc ?? DateTime.UtcNow,
            PaymentMethod = request.PaymentMethod,
            Reference = request.Reference,
            Notes = request.Notes,
        };
        _db.VendorPayments.Add(payment);

        // Check if fully paid
        var totalPaid = invoice.Payments.Sum(p => p.PaidAmount) + request.PaidAmount;
        if (totalPaid >= invoice.Amount)
        {
            invoice.Status = VendorInvoiceStatus.Paid;
        }

        await _db.SaveChangesAsync();
        return Ok(MapPaymentDto(payment));
    }

    [HttpPut("payments/{paymentId}")]
    public async Task<IActionResult> UpdatePayment(int paymentId, [FromBody] CreateVendorPaymentRequest request)
    {
        var payment = await _db.VendorPayments.FindAsync(paymentId);
        if (payment == null) return NotFound();

        payment.PaidAmount = request.PaidAmount;
        payment.PaidAtUtc = request.PaidAtUtc ?? payment.PaidAtUtc;
        payment.PaymentMethod = request.PaymentMethod;
        payment.Reference = request.Reference;
        payment.Notes = request.Notes;

        // Re-check invoice status
        var invoice = await _db.VendorInvoices.Include(v => v.Payments).FirstAsync(v => v.Id == payment.VendorInvoiceId);
        var totalPaid = invoice.Payments.Sum(p => p.PaidAmount);
        invoice.Status = totalPaid >= invoice.Amount ? VendorInvoiceStatus.Paid
            : invoice.Status == VendorInvoiceStatus.Paid ? VendorInvoiceStatus.Approved
            : invoice.Status;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("payments/{paymentId}")]
    public async Task<IActionResult> DeletePayment(int paymentId)
    {
        var payment = await _db.VendorPayments.FindAsync(paymentId);
        if (payment == null) return NotFound();

        var invoiceId = payment.VendorInvoiceId;
        _db.VendorPayments.Remove(payment);
        await _db.SaveChangesAsync();

        // Re-check invoice status
        var invoice = await _db.VendorInvoices.Include(v => v.Payments).FirstAsync(v => v.Id == invoiceId);
        var totalPaid = invoice.Payments.Sum(p => p.PaidAmount);
        if (totalPaid < invoice.Amount && invoice.Status == VendorInvoiceStatus.Paid)
        {
            invoice.Status = VendorInvoiceStatus.Approved;
            await _db.SaveChangesAsync();
        }

        return NoContent();
    }

    // ─── Mapping ─────────────────────────────────────────

    private static VendorInvoiceDto MapDto(VendorInvoice vi)
    {
        var paidAmount = vi.Payments?.Sum(p => p.PaidAmount) ?? 0;
        return new VendorInvoiceDto
        {
            Id = vi.Id,
            BuildingId = vi.BuildingId,
            BuildingName = vi.Building?.Name,
            VendorId = vi.VendorId,
            VendorName = vi.Vendor?.Name,
            WorkOrderId = vi.WorkOrderId,
            ServiceRequestId = vi.ServiceRequestId,
            Category = vi.Category,
            Description = vi.Description,
            InvoiceNumber = vi.InvoiceNumber,
            InvoiceDate = vi.InvoiceDate,
            Amount = vi.Amount,
            PaidAmount = paidAmount,
            Balance = vi.Amount - paidAmount,
            DueDate = vi.DueDate,
            Status = vi.Status,
            Notes = vi.Notes,
            CreatedAtUtc = vi.CreatedAtUtc,
        };
    }

    private static VendorPaymentDto MapPaymentDto(VendorPayment vp) => new()
    {
        Id = vp.Id,
        VendorInvoiceId = vp.VendorInvoiceId,
        PaidAmount = vp.PaidAmount,
        PaidAtUtc = vp.PaidAtUtc,
        PaymentMethod = vp.PaymentMethod,
        Reference = vp.Reference,
        Notes = vp.Notes,
        CreatedAtUtc = vp.CreatedAtUtc,
    };
}
