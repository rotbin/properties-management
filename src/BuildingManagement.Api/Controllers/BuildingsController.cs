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
[Route("api/buildings")]
[Authorize]
public class BuildingsController : ControllerBase
{
    private readonly AppDbContext _db;

    public BuildingsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<List<BuildingDto>>> GetAll()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var isAdmin = User.IsInRole(AppRoles.Admin);
        var isManager = User.IsInRole(AppRoles.Manager);

        IQueryable<Building> query = _db.Buildings.Include(b => b.Units);

        // Manager can only see buildings they manage
        if (isManager && !isAdmin)
        {
            var buildingIds = await _db.BuildingManagers
                .Where(bm => bm.UserId == userId)
                .Select(bm => bm.BuildingId)
                .ToListAsync();
            query = query.Where(b => buildingIds.Contains(b.Id));
        }

        // Tenant can only see buildings they belong to
        if (User.IsInRole(AppRoles.Tenant) && !isAdmin && !isManager)
        {
            var tenantBuildingIds = await _db.TenantProfiles
                .Where(tp => tp.UserId == userId && tp.IsActive && !tp.IsDeleted)
                .Select(tp => tp.Unit.BuildingId)
                .Distinct()
                .ToListAsync();
            if (tenantBuildingIds.Count > 0)
                query = query.Where(b => tenantBuildingIds.Contains(b.Id));
        }

        var buildings = await query.Select(b => new BuildingDto
        {
            Id = b.Id,
            Name = b.Name,
            AddressLine = b.AddressLine,
            City = b.City,
            PostalCode = b.PostalCode,
            Notes = b.Notes,
            UnitCount = b.Units.Count(u => !u.IsDeleted)
        }).ToListAsync();

        return Ok(buildings);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<BuildingDto>> GetById(int id)
    {
        var building = await _db.Buildings.Include(b => b.Units)
            .FirstOrDefaultAsync(b => b.Id == id);
        if (building == null) return NotFound();

        return Ok(new BuildingDto
        {
            Id = building.Id,
            Name = building.Name,
            AddressLine = building.AddressLine,
            City = building.City,
            PostalCode = building.PostalCode,
            Notes = building.Notes,
            UnitCount = building.Units.Count(u => !u.IsDeleted)
        });
    }

    [HttpPost]
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<ActionResult<BuildingDto>> Create([FromBody] CreateBuildingRequest request)
    {
        var building = new Building
        {
            Name = request.Name,
            AddressLine = request.AddressLine,
            City = request.City,
            PostalCode = request.PostalCode,
            Notes = request.Notes,
            CreatedBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        };

        _db.Buildings.Add(building);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = building.Id }, new BuildingDto
        {
            Id = building.Id,
            Name = building.Name,
            AddressLine = building.AddressLine,
            City = building.City,
            PostalCode = building.PostalCode,
            Notes = building.Notes
        });
    }

    [HttpPut("{id}")]
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateBuildingRequest request)
    {
        var building = await _db.Buildings.FindAsync(id);
        if (building == null) return NotFound();

        building.Name = request.Name;
        building.AddressLine = request.AddressLine;
        building.City = request.City;
        building.PostalCode = request.PostalCode;
        building.Notes = request.Notes;
        building.UpdatedBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> Delete(int id)
    {
        var building = await _db.Buildings.FindAsync(id);
        if (building == null) return NotFound();

        building.IsDeleted = true;
        building.UpdatedBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpGet("{id}/units")]
    public async Task<ActionResult<List<UnitDto>>> GetUnits(int id)
    {
        var units = await _db.Units
            .Where(u => u.BuildingId == id)
            .Include(u => u.TenantUser)
            .Select(u => new UnitDto
            {
                Id = u.Id,
                BuildingId = u.BuildingId,
                UnitNumber = u.UnitNumber,
                Floor = u.Floor,
                SizeSqm = u.SizeSqm,
                OwnerName = u.OwnerName,
                TenantUserId = u.TenantUserId,
                TenantName = u.TenantUser != null ? u.TenantUser.FullName : null
            }).ToListAsync();

        return Ok(units);
    }

    [HttpPost("{id}/units")]
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<ActionResult<UnitDto>> CreateUnit(int id, [FromBody] CreateUnitRequest request)
    {
        var building = await _db.Buildings.FindAsync(id);
        if (building == null) return NotFound();

        var unit = new Unit
        {
            BuildingId = id,
            UnitNumber = request.UnitNumber,
            Floor = request.Floor,
            SizeSqm = request.SizeSqm,
            OwnerName = request.OwnerName,
            TenantUserId = request.TenantUserId,
            CreatedBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        };

        _db.Units.Add(unit);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetUnits), new { id }, new UnitDto
        {
            Id = unit.Id,
            BuildingId = unit.BuildingId,
            UnitNumber = unit.UnitNumber,
            Floor = unit.Floor,
            SizeSqm = unit.SizeSqm,
            OwnerName = unit.OwnerName,
            TenantUserId = unit.TenantUserId
        });
    }
}
