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
[Route("api/hoa")]
[Authorize]
public class HOAController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHOAFeeService _hoaFeeService;

    public HOAController(AppDbContext db, IHOAFeeService hoaFeeService)
    {
        _db = db;
        _hoaFeeService = hoaFeeService;
    }

    // ─── Plans ──────────────────────────────────────────

    [HttpGet("plans/{buildingId}")]
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<ActionResult<List<HOAFeePlanDto>>> GetPlans(int buildingId)
    {
        var plans = await _db.HOAFeePlans
            .Where(p => p.BuildingId == buildingId)
            .Include(p => p.Building)
            .OrderByDescending(p => p.EffectiveFrom)
            .Select(p => new HOAFeePlanDto
            {
                Id = p.Id,
                BuildingId = p.BuildingId,
                BuildingName = p.Building.Name,
                Name = p.Name,
                CalculationMethod = p.CalculationMethod,
                AmountPerSqm = p.AmountPerSqm,
                FixedAmountPerUnit = p.FixedAmountPerUnit,
                EffectiveFrom = p.EffectiveFrom,
                IsActive = p.IsActive
            }).ToListAsync();
        return Ok(plans);
    }

    [HttpPost("plans")]
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<ActionResult<HOAFeePlanDto>> CreatePlan([FromBody] CreateHOAFeePlanRequest request)
    {
        var plan = new HOAFeePlan
        {
            BuildingId = request.BuildingId,
            Name = request.Name,
            CalculationMethod = request.CalculationMethod,
            AmountPerSqm = request.AmountPerSqm,
            FixedAmountPerUnit = request.FixedAmountPerUnit,
            EffectiveFrom = request.EffectiveFrom,
            IsActive = true,
            CreatedBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        };

        _db.HOAFeePlans.Add(plan);
        await _db.SaveChangesAsync();

        var building = await _db.Buildings.FindAsync(plan.BuildingId);
        return Ok(new HOAFeePlanDto
        {
            Id = plan.Id,
            BuildingId = plan.BuildingId,
            BuildingName = building?.Name,
            Name = plan.Name,
            CalculationMethod = plan.CalculationMethod,
            AmountPerSqm = plan.AmountPerSqm,
            FixedAmountPerUnit = plan.FixedAmountPerUnit,
            EffectiveFrom = plan.EffectiveFrom,
            IsActive = plan.IsActive
        });
    }

    [HttpPut("plans/{id}")]
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<IActionResult> UpdatePlan(int id, [FromBody] UpdateHOAFeePlanRequest request)
    {
        var plan = await _db.HOAFeePlans.FindAsync(id);
        if (plan == null) return NotFound();

        plan.Name = request.Name;
        plan.CalculationMethod = request.CalculationMethod;
        plan.AmountPerSqm = request.AmountPerSqm;
        plan.FixedAmountPerUnit = request.FixedAmountPerUnit;
        plan.IsActive = request.IsActive;
        plan.UpdatedBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("plans/{id}/generate/{period}")]
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<ActionResult<GenerateChargesResult>> GenerateCharges(int id, string period)
    {
        var plan = await _db.HOAFeePlans.FindAsync(id);
        if (plan == null) return NotFound();

        var result = await _hoaFeeService.GenerateMonthlyChargesAsync(plan.BuildingId, period);
        return Ok(result);
    }

    // ─── Charges ────────────────────────────────────────

    [HttpGet("charges")]
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<ActionResult<List<UnitChargeDto>>> GetCharges(
        [FromQuery] int? buildingId,
        [FromQuery] string? period)
    {
        IQueryable<UnitCharge> query = _db.UnitCharges
            .Include(uc => uc.Unit).ThenInclude(u => u.TenantUser)
            .Include(uc => uc.Allocations);

        if (buildingId.HasValue)
            query = query.Where(uc => uc.Unit.BuildingId == buildingId);
        if (!string.IsNullOrEmpty(period))
            query = query.Where(uc => uc.Period == period);

        var items = await query.OrderBy(uc => uc.Unit.UnitNumber).ToListAsync();
        return Ok(items.Select(MapChargeDto).ToList());
    }

    [HttpGet("charges/unit/{unitId}")]
    public async Task<ActionResult<List<UnitChargeDto>>> GetChargesForUnit(int unitId)
    {
        // Tenant can only see own unit charges
        if (User.IsInRole(AppRoles.Tenant))
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
            var unit = await _db.Units.FindAsync(unitId);
            if (unit?.TenantUserId != userId) return Forbid();
        }

        var items = await _db.UnitCharges
            .Include(uc => uc.Unit).ThenInclude(u => u.TenantUser)
            .Include(uc => uc.Allocations)
            .Where(uc => uc.UnitId == unitId)
            .OrderByDescending(uc => uc.Period)
            .ToListAsync();

        return Ok(items.Select(MapChargeDto).ToList());
    }

    [HttpGet("charges/my")]
    [Authorize(Roles = AppRoles.Tenant)]
    public async Task<ActionResult<List<UnitChargeDto>>> GetMyCharges()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
        var unitIds = await _db.Units
            .Where(u => u.TenantUserId == userId)
            .Select(u => u.Id)
            .ToListAsync();

        var items = await _db.UnitCharges
            .Include(uc => uc.Unit).ThenInclude(u => u.TenantUser)
            .Include(uc => uc.Allocations)
            .Where(uc => unitIds.Contains(uc.UnitId))
            .OrderByDescending(uc => uc.Period)
            .ToListAsync();

        return Ok(items.Select(MapChargeDto).ToList());
    }

    [HttpPut("charges/{id}/adjust")]
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<IActionResult> AdjustCharge(int id, [FromBody] AdjustChargeRequest request)
    {
        var charge = await _db.UnitCharges.Include(uc => uc.Unit).FirstOrDefaultAsync(uc => uc.Id == id);
        if (charge == null) return NotFound();

        var oldAmount = charge.AmountDue;
        charge.AmountDue = request.NewAmount;

        // Recalculate status based on allocations
        var totalPaid = await _db.PaymentAllocations
            .Where(pa => pa.UnitChargeId == id)
            .SumAsync(pa => pa.AllocatedAmount);

        charge.Status = totalPaid >= request.NewAmount
            ? UnitChargeStatus.Paid
            : totalPaid > 0
                ? UnitChargeStatus.PartiallyPaid
                : UnitChargeStatus.Pending;

        // Ledger adjustment
        var diff = request.NewAmount - oldAmount;
        if (diff != 0)
        {
            var lastBalance = await _db.LedgerEntries
                .Where(le => le.UnitId == charge.UnitId)
                .OrderByDescending(le => le.Id)
                .Select(le => (decimal?)le.BalanceAfter)
                .FirstOrDefaultAsync() ?? 0m;

            _db.LedgerEntries.Add(new LedgerEntry
            {
                BuildingId = charge.Unit.BuildingId,
                UnitId = charge.UnitId,
                EntryType = LedgerEntryType.Adjustment,
                ReferenceId = charge.Id,
                Debit = diff > 0 ? diff : 0,
                Credit = diff < 0 ? -diff : 0,
                BalanceAfter = lastBalance + diff
            });
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ─── Manual Payment CRUD ──────────────────────────────

    [HttpPost("charges/{unitChargeId}/manual-payment")]
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<ActionResult<ManualPaymentResultDto>> AddManualPayment(int unitChargeId, [FromBody] ManualPaymentRequest request)
    {
        if (request.PaidAmount <= 0)
            return BadRequest(new { message = "Amount must be greater than zero." });

        var charge = await _db.UnitCharges
            .Include(uc => uc.Unit)
            .Include(uc => uc.Allocations)
            .FirstOrDefaultAsync(uc => uc.Id == unitChargeId);
        if (charge == null) return NotFound();

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;

        // Check building access for managers
        if (!User.IsInRole(AppRoles.Admin))
        {
            var hasAccess = await _db.BuildingManagers
                .AnyAsync(bm => bm.UserId == userId && bm.BuildingId == charge.Unit.BuildingId);
            if (!hasAccess) return Forbid();
        }

        // Check for overpayment
        var currentPaid = charge.Allocations?.Sum(a => a.AllocatedAmount) ?? 0;
        var outstanding = charge.AmountDue - currentPaid;
        if (request.PaidAmount > outstanding)
            return BadRequest(new { message = $"Amount ({request.PaidAmount:F2}) exceeds outstanding balance ({outstanding:F2})." });

        // Create Payment record
        var payment = new Payment
        {
            UnitId = charge.UnitId,
            UserId = userId,
            Amount = request.PaidAmount,
            PaymentDateUtc = request.PaidAt ?? DateTime.UtcNow,
            Status = PaymentStatus.Succeeded,
            IsManual = true,
            ManualMethodType = request.Method,
            ProviderReference = request.Reference,
            Notes = request.Notes,
            EnteredByUserId = userId
        };
        _db.Payments.Add(payment);
        await _db.SaveChangesAsync();

        // Create allocation
        _db.PaymentAllocations.Add(new PaymentAllocation
        {
            PaymentId = payment.Id,
            UnitChargeId = unitChargeId,
            AllocatedAmount = request.PaidAmount
        });

        // Update charge status
        var newTotalPaid = currentPaid + request.PaidAmount;
        charge.Status = newTotalPaid >= charge.AmountDue
            ? UnitChargeStatus.Paid
            : UnitChargeStatus.PartiallyPaid;

        // Ledger entry
        _db.LedgerEntries.Add(new LedgerEntry
        {
            BuildingId = charge.Unit.BuildingId,
            UnitId = charge.UnitId,
            EntryType = LedgerEntryType.Payment,
            Category = "HOAMonthlyFees",
            Description = $"Manual payment ({request.Method}): {request.Reference ?? ""}",
            ReferenceId = payment.Id,
            Debit = 0,
            Credit = request.PaidAmount,
            BalanceAfter = 0,
            CreatedAtUtc = DateTime.UtcNow
        });

        // Audit log
        _db.AuditLogs.Add(new AuditLog
        {
            Action = "ManualPayment",
            EntityName = "UnitCharge",
            EntityId = unitChargeId.ToString(),
            PerformedBy = userId,
            Details = $"Added manual payment of {request.PaidAmount:F2} via {request.Method}. Ref: {request.Reference}. Notes: {request.Notes}. Old paid: {currentPaid:F2}, New paid: {newTotalPaid:F2}, Status: {charge.Status}"
        });

        await _db.SaveChangesAsync();

        return Ok(new ManualPaymentResultDto
        {
            PaymentId = payment.Id,
            UnitChargeId = unitChargeId,
            AmountDue = charge.AmountDue,
            AmountPaid = newTotalPaid,
            Outstanding = charge.AmountDue - newTotalPaid,
            Status = charge.Status,
            LastPaymentDate = payment.PaymentDateUtc
        });
    }

    [HttpPut("manual-payments/{paymentId}")]
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<IActionResult> EditManualPayment(int paymentId, [FromBody] ManualPaymentRequest request)
    {
        var payment = await _db.Payments
            .Include(p => p.Allocations)
            .FirstOrDefaultAsync(p => p.Id == paymentId);
        if (payment == null) return NotFound();
        if (!payment.IsManual)
            return BadRequest(new { message = "Only manual payments can be edited." });

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
        var oldAmount = payment.Amount;

        // Get linked charge via allocation
        var allocation = payment.Allocations.FirstOrDefault();
        if (allocation == null) return BadRequest(new { message = "No allocation found for this payment." });

        var charge = await _db.UnitCharges
            .Include(uc => uc.Allocations)
            .FirstOrDefaultAsync(uc => uc.Id == allocation.UnitChargeId);
        if (charge == null) return NotFound();

        // Check new amount won't cause overpayment
        var otherPaid = (charge.Allocations?.Sum(a => a.AllocatedAmount) ?? 0) - allocation.AllocatedAmount;
        var maxAllowed = charge.AmountDue - otherPaid;
        if (request.PaidAmount > maxAllowed)
            return BadRequest(new { message = $"Amount ({request.PaidAmount:F2}) exceeds outstanding balance ({maxAllowed:F2})." });

        // Update payment
        payment.Amount = request.PaidAmount;
        payment.PaymentDateUtc = request.PaidAt ?? payment.PaymentDateUtc;
        payment.ManualMethodType = request.Method;
        payment.ProviderReference = request.Reference;
        payment.Notes = request.Notes;

        // Update allocation
        allocation.AllocatedAmount = request.PaidAmount;

        // Update charge status
        var newTotalPaid = otherPaid + request.PaidAmount;
        charge.Status = newTotalPaid >= charge.AmountDue
            ? UnitChargeStatus.Paid
            : newTotalPaid > 0
                ? UnitChargeStatus.PartiallyPaid
                : UnitChargeStatus.Pending;

        // Audit log
        _db.AuditLogs.Add(new AuditLog
        {
            Action = "EditManualPayment",
            EntityName = "Payment",
            EntityId = paymentId.ToString(),
            PerformedBy = userId,
            Details = $"Edited manual payment. Old amount: {oldAmount:F2}, New amount: {request.PaidAmount:F2}. Method: {request.Method}. Ref: {request.Reference}"
        });

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("manual-payments/{paymentId}")]
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<IActionResult> DeleteManualPayment(int paymentId)
    {
        var payment = await _db.Payments
            .Include(p => p.Allocations)
            .FirstOrDefaultAsync(p => p.Id == paymentId);
        if (payment == null) return NotFound();
        if (!payment.IsManual)
            return BadRequest(new { message = "Only manual payments can be removed." });

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
        var allocation = payment.Allocations.FirstOrDefault();

        // Mark payment as cancelled (no hard delete)
        payment.Status = PaymentStatus.Cancelled;
        payment.Notes = (payment.Notes ?? "") + $" [Cancelled by {userId} at {DateTime.UtcNow:u}]";

        // Zero out the allocation
        if (allocation != null)
        {
            var charge = await _db.UnitCharges
                .Include(uc => uc.Allocations)
                .FirstOrDefaultAsync(uc => uc.Id == allocation.UnitChargeId);

            allocation.AllocatedAmount = 0;

            if (charge != null)
            {
                var newTotalPaid = (charge.Allocations?.Sum(a => a.AllocatedAmount) ?? 0) - payment.Amount;
                if (newTotalPaid < 0) newTotalPaid = 0;
                charge.Status = newTotalPaid >= charge.AmountDue
                    ? UnitChargeStatus.Paid
                    : newTotalPaid > 0
                        ? UnitChargeStatus.PartiallyPaid
                        : (charge.DueDate < DateTime.UtcNow ? UnitChargeStatus.Overdue : UnitChargeStatus.Pending);

                // Reversal ledger entry
                _db.LedgerEntries.Add(new LedgerEntry
                {
                    BuildingId = charge.Unit?.BuildingId ?? 0,
                    UnitId = charge.UnitId,
                    EntryType = LedgerEntryType.Adjustment,
                    Category = "PaymentReversal",
                    Description = $"Cancelled manual payment #{paymentId}",
                    ReferenceId = paymentId,
                    Debit = payment.Amount,
                    Credit = 0,
                    BalanceAfter = 0,
                    CreatedAtUtc = DateTime.UtcNow
                });
            }
        }

        // Audit log
        _db.AuditLogs.Add(new AuditLog
        {
            Action = "CancelManualPayment",
            EntityName = "Payment",
            EntityId = paymentId.ToString(),
            PerformedBy = userId,
            Details = $"Cancelled manual payment of {payment.Amount:F2}."
        });

        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Get all payments for a specific charge.</summary>
    [HttpGet("charges/{unitChargeId}/payments")]
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<ActionResult<List<ChargePaymentDto>>> GetChargePayments(int unitChargeId)
    {
        var allocations = await _db.PaymentAllocations
            .Include(pa => pa.Payment)
            .Where(pa => pa.UnitChargeId == unitChargeId && pa.AllocatedAmount > 0)
            .OrderByDescending(pa => pa.Payment.PaymentDateUtc)
            .ToListAsync();

        var result = new List<ChargePaymentDto>();
        foreach (var a in allocations)
        {
            string? enteredByName = null;
            if (a.Payment.EnteredByUserId != null)
            {
                var enteredBy = await _db.Users.FindAsync(a.Payment.EnteredByUserId);
                enteredByName = enteredBy?.FullName ?? enteredBy?.Email;
            }
            result.Add(new ChargePaymentDto
            {
                Id = a.Payment.Id,
                Amount = a.AllocatedAmount,
                PaymentDateUtc = a.Payment.PaymentDateUtc,
                IsManual = a.Payment.IsManual,
                ManualMethodType = a.Payment.ManualMethodType,
                ProviderReference = a.Payment.ProviderReference,
                Notes = a.Payment.Notes,
                EnteredByName = enteredByName,
                Status = a.Payment.Status,
                CreatedAtUtc = a.Payment.CreatedAtUtc
            });
        }
        return Ok(result);
    }

    private static UnitChargeDto MapChargeDto(UnitCharge uc)
    {
        var paid = uc.Allocations?.Sum(a => a.AllocatedAmount) ?? 0;
        return new UnitChargeDto
        {
            Id = uc.Id,
            UnitId = uc.UnitId,
            UnitNumber = uc.Unit?.UnitNumber,
            Floor = uc.Unit?.Floor,
            TenantName = uc.Unit?.TenantUser?.FullName ?? uc.Unit?.OwnerName,
            HOAFeePlanId = uc.HOAFeePlanId,
            Period = uc.Period,
            AmountDue = uc.AmountDue,
            AmountPaid = paid,
            Balance = uc.AmountDue - paid,
            DueDate = uc.DueDate,
            Status = uc.Status,
            CreatedAtUtc = uc.CreatedAtUtc
        };
    }
}
