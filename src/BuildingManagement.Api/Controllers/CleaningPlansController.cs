using BuildingManagement.Core.DTOs;
using BuildingManagement.Core.Entities;
using BuildingManagement.Core.Enums;
using BuildingManagement.Infrastructure.Data;
using BuildingManagement.Infrastructure.Jobs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BuildingManagement.Api.Controllers;

[ApiController]
[Route("api/cleaningplans")]
[Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
public class CleaningPlansController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly MaintenanceJobService _jobService;

    public CleaningPlansController(AppDbContext db, MaintenanceJobService jobService)
    {
        _db = db;
        _jobService = jobService;
    }

    [HttpGet("{buildingId}")]
    public async Task<ActionResult<CleaningPlanDto?>> GetByBuilding(int buildingId)
    {
        var plan = await _db.CleaningPlans
            .Include(cp => cp.CleaningVendor)
            .Where(cp => cp.BuildingId == buildingId)
            .OrderByDescending(cp => cp.EffectiveFrom)
            .FirstOrDefaultAsync();

        if (plan == null) return Ok((CleaningPlanDto?)null);

        return Ok(new CleaningPlanDto
        {
            Id = plan.Id,
            BuildingId = plan.BuildingId,
            CleaningVendorId = plan.CleaningVendorId,
            CleaningVendorName = plan.CleaningVendor.Name,
            StairwellsPerWeek = plan.StairwellsPerWeek,
            ParkingPerWeek = plan.ParkingPerWeek,
            CorridorLobbyPerWeek = plan.CorridorLobbyPerWeek,
            GarbageRoomPerWeek = plan.GarbageRoomPerWeek,
            EffectiveFrom = plan.EffectiveFrom
        });
    }

    [HttpPost("{buildingId}")]
    public async Task<ActionResult<CleaningPlanDto>> Create(int buildingId, [FromBody] CreateCleaningPlanRequest request)
    {
        var building = await _db.Buildings.FindAsync(buildingId);
        if (building == null) return NotFound(new { message = "Building not found." });

        var vendor = await _db.Vendors.FindAsync(request.CleaningVendorId);
        if (vendor == null) return NotFound(new { message = "Vendor not found." });

        var plan = new CleaningPlan
        {
            BuildingId = buildingId,
            CleaningVendorId = request.CleaningVendorId,
            StairwellsPerWeek = request.StairwellsPerWeek,
            ParkingPerWeek = request.ParkingPerWeek,
            CorridorLobbyPerWeek = request.CorridorLobbyPerWeek,
            GarbageRoomPerWeek = request.GarbageRoomPerWeek,
            EffectiveFrom = request.EffectiveFrom,
            CreatedBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        };

        _db.CleaningPlans.Add(plan);
        await _db.SaveChangesAsync();

        return Ok(new CleaningPlanDto
        {
            Id = plan.Id,
            BuildingId = plan.BuildingId,
            CleaningVendorId = plan.CleaningVendorId,
            CleaningVendorName = vendor.Name,
            StairwellsPerWeek = plan.StairwellsPerWeek,
            ParkingPerWeek = plan.ParkingPerWeek,
            CorridorLobbyPerWeek = plan.CorridorLobbyPerWeek,
            GarbageRoomPerWeek = plan.GarbageRoomPerWeek,
            EffectiveFrom = plan.EffectiveFrom
        });
    }

    [HttpPost("{buildingId}/generate-weekly")]
    public async Task<ActionResult<GenerateJobResponse>> GenerateWeekly(int buildingId)
    {
        var (alreadyRan, periodKey, created) = await _jobService.GenerateCleaningWorkOrdersAsync(buildingId);
        return Ok(new GenerateJobResponse
        {
            AlreadyRan = alreadyRan,
            PeriodKey = periodKey,
            WorkOrdersCreated = created,
            Message = alreadyRan ? "Already generated for this week." : $"Created {created} cleaning work orders."
        });
    }
}
