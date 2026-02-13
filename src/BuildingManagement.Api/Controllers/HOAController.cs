using BuildingManagement.Core.DTOs;
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
