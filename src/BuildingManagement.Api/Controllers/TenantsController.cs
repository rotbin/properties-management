using BuildingManagement.Core.DTOs;
using BuildingManagement.Core.Entities;
using BuildingManagement.Core.Enums;
using BuildingManagement.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BuildingManagement.Api.Controllers;

[ApiController]
[Route("api/tenants")]
[Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
public class TenantsController : ControllerBase
{
    private readonly AppDbContext _db;

    public TenantsController(AppDbContext db) => _db = db;

    // ─── MY PROFILE (tenant) ────────────────────────────

    [HttpGet("my-profile")]
    [Authorize(Roles = AppRoles.Tenant)]
    public async Task<ActionResult<TenantProfileDto>> GetMyProfile()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
        var tp = await _db.TenantProfiles
            .Include(t => t.Unit).ThenInclude(u => u.Building)
            .Where(t => t.UserId == userId && t.IsActive && !t.IsDeleted)
            .FirstOrDefaultAsync();
        if (tp == null) return NotFound();
        return Ok(MapDto(tp));
    }

    // ─── LIST ────────────────────────────────────────────

    [HttpGet]
    public async Task<ActionResult<List<TenantProfileDto>>> GetAll(
        [FromQuery] int? buildingId,
        [FromQuery] int? unitId,
        [FromQuery] bool? activeOnly,
        [FromQuery] bool includeArchived = false)
    {
        IQueryable<TenantProfile> query = _db.TenantProfiles
            .Include(tp => tp.Unit).ThenInclude(u => u.Building)
            .Include(tp => tp.User);

        if (buildingId.HasValue)
            query = query.Where(tp => tp.Unit.BuildingId == buildingId);
        if (unitId.HasValue)
            query = query.Where(tp => tp.UnitId == unitId);
        if (activeOnly == true)
            query = query.Where(tp => tp.IsActive);
        if (!includeArchived)
            query = query.Where(tp => !tp.IsArchived);

        var tenants = await query
            .OrderByDescending(tp => tp.IsActive)
            .ThenByDescending(tp => tp.MoveInDate)
            .Select(tp => MapDto(tp))
            .ToListAsync();

        return Ok(tenants);
    }

    // ─── GET BY ID ───────────────────────────────────────

    [HttpGet("{id}")]
    public async Task<ActionResult<TenantProfileDto>> GetById(int id)
    {
        var tp = await _db.TenantProfiles
            .Include(t => t.Unit).ThenInclude(u => u.Building)
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (tp == null) return NotFound();
        return Ok(MapDto(tp));
    }

    // ─── CREATE ──────────────────────────────────────────

    [HttpPost]
    public async Task<ActionResult<TenantProfileDto>> Create([FromBody] CreateTenantRequest request)
    {
        var unit = await _db.Units.Include(u => u.Building).FirstOrDefaultAsync(u => u.Id == request.UnitId);
        if (unit == null) return BadRequest(new { message = "Unit not found." });

        // If creating as active, end current active tenant
        if (request.IsActive)
        {
            await EndActiveTenantsForUnit(request.UnitId);
        }

        var tenant = new TenantProfile
        {
            UnitId = request.UnitId,
            UserId = request.UserId,
            FullName = request.FullName,
            Phone = request.Phone,
            Email = request.Email,
            MoveInDate = request.MoveInDate ?? DateTime.UtcNow,
            IsActive = request.IsActive,
            Notes = request.Notes,
            CreatedBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        };

        _db.TenantProfiles.Add(tenant);

        // Also update Unit.TenantUserId if we have a user and the tenant is active
        if (request.IsActive && !string.IsNullOrEmpty(request.UserId))
        {
            unit.TenantUserId = request.UserId;
        }
        // Update Unit.OwnerName to the tenant name for display purposes
        if (request.IsActive)
        {
            unit.OwnerName = request.FullName;
        }

        await _db.SaveChangesAsync();

        // Re-fetch to include navigation properties
        var created = await _db.TenantProfiles
            .Include(t => t.Unit).ThenInclude(u => u.Building)
            .Include(t => t.User)
            .FirstAsync(t => t.Id == tenant.Id);

        return Ok(MapDto(created));
    }

    // ─── UPDATE ──────────────────────────────────────────

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateTenantRequest request)
    {
        var tenant = await _db.TenantProfiles
            .Include(t => t.Unit)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (tenant == null) return NotFound();

        // If setting active, end other active tenants for this unit
        if (request.IsActive && !tenant.IsActive)
        {
            await EndActiveTenantsForUnit(tenant.UnitId);
        }

        tenant.FullName = request.FullName;
        tenant.Phone = request.Phone;
        tenant.Email = request.Email;
        tenant.MoveInDate = request.MoveInDate;
        tenant.MoveOutDate = request.MoveOutDate;
        tenant.IsActive = request.IsActive;
        tenant.Notes = request.Notes;
        tenant.UpdatedBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        // Update Unit.OwnerName if active
        if (request.IsActive)
        {
            tenant.Unit.OwnerName = request.FullName;
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ─── END TENANCY ─────────────────────────────────────

    [HttpPost("{id}/end-tenancy")]
    public async Task<IActionResult> EndTenancy(int id, [FromBody] EndTenancyRequest request)
    {
        var tenant = await _db.TenantProfiles
            .Include(t => t.Unit)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (tenant == null) return NotFound();

        tenant.MoveOutDate = request.MoveOutDate;
        tenant.IsActive = false;
        tenant.UpdatedBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        // Clear unit's tenant user link if it points to this tenant
        if (tenant.Unit.TenantUserId == tenant.UserId && tenant.UserId != null)
        {
            tenant.Unit.TenantUserId = null;
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ─── DELETE (soft or hard) ───────────────────────────

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var tenant = await _db.TenantProfiles.FindAsync(id);
        if (tenant == null) return NotFound();

        // Check if tenant has related records
        var hasCharges = await _db.UnitCharges.AnyAsync(uc => uc.UnitId == tenant.UnitId);
        var hasPayments = await _db.Payments.AnyAsync(p => p.UnitId == tenant.UnitId);
        var hasServiceRequests = tenant.UserId != null &&
            await _db.ServiceRequests.AnyAsync(sr => sr.SubmittedByUserId == tenant.UserId);

        if (hasCharges || hasPayments || hasServiceRequests)
        {
            // Soft delete: archive
            tenant.IsArchived = true;
            tenant.IsActive = false;
            tenant.MoveOutDate ??= DateTime.UtcNow;
            tenant.UpdatedBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            await _db.SaveChangesAsync();
            return Ok(new { archived = true, message = "Tenant archived (has related records)." });
        }
        else
        {
            // Hard delete (mark IsDeleted for query filter)
            tenant.IsDeleted = true;
            tenant.IsActive = false;
            await _db.SaveChangesAsync();
            return Ok(new { archived = false, message = "Tenant deleted." });
        }
    }

    // ─── UNIT TENANT HISTORY ─────────────────────────────

    [HttpGet("unit/{unitId}/history")]
    public async Task<ActionResult<List<TenantProfileDto>>> UnitHistory(int unitId)
    {
        // Include archived tenants by ignoring the soft-delete query filter
        var history = await _db.TenantProfiles
            .IgnoreQueryFilters()
            .Include(tp => tp.Unit).ThenInclude(u => u.Building)
            .Include(tp => tp.User)
            .Where(tp => tp.UnitId == unitId)
            .OrderByDescending(tp => tp.MoveInDate)
            .Select(tp => MapDto(tp))
            .ToListAsync();

        return Ok(history);
    }

    // ─── Helper: end all active tenants for a unit ───────

    private async Task EndActiveTenantsForUnit(int unitId)
    {
        var activeTenants = await _db.TenantProfiles
            .Where(tp => tp.UnitId == unitId && tp.IsActive)
            .ToListAsync();

        foreach (var t in activeTenants)
        {
            t.IsActive = false;
            t.MoveOutDate ??= DateTime.UtcNow;
        }
    }

    private static TenantProfileDto MapDto(TenantProfile tp) => new()
    {
        Id = tp.Id,
        UnitId = tp.UnitId,
        UnitNumber = tp.Unit?.UnitNumber,
        Floor = tp.Unit?.Floor,
        BuildingId = tp.Unit?.BuildingId ?? 0,
        BuildingName = tp.Unit?.Building?.Name,
        UserId = tp.UserId,
        FullName = tp.FullName,
        Phone = tp.Phone,
        Email = tp.Email,
        MoveInDate = tp.MoveInDate,
        MoveOutDate = tp.MoveOutDate,
        IsActive = tp.IsActive,
        IsArchived = tp.IsArchived,
        Notes = tp.Notes,
        CreatedAtUtc = tp.CreatedAtUtc
    };
}
