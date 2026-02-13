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
[Route("api")]
[Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
public class AssetsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly MaintenanceJobService _jobService;

    public AssetsController(AppDbContext db, MaintenanceJobService jobService)
    {
        _db = db;
        _jobService = jobService;
    }

    [HttpGet("assets")]
    public async Task<ActionResult<List<AssetDto>>> GetAssets([FromQuery] int? buildingId)
    {
        IQueryable<Asset> query = _db.Assets
            .Include(a => a.Building)
            .Include(a => a.Vendor);

        if (buildingId.HasValue)
            query = query.Where(a => a.BuildingId == buildingId);

        var items = await query.Select(a => new AssetDto
        {
            Id = a.Id,
            BuildingId = a.BuildingId,
            BuildingName = a.Building.Name,
            Name = a.Name,
            AssetType = a.AssetType,
            LocationDescription = a.LocationDescription,
            SerialNumber = a.SerialNumber,
            InstallDate = a.InstallDate,
            WarrantyUntil = a.WarrantyUntil,
            VendorId = a.VendorId,
            VendorName = a.Vendor != null ? a.Vendor.Name : null,
            Notes = a.Notes
        }).ToListAsync();

        return Ok(items);
    }

    [HttpPost("assets")]
    public async Task<ActionResult<AssetDto>> CreateAsset([FromBody] CreateAssetRequest request)
    {
        var asset = new Asset
        {
            BuildingId = request.BuildingId,
            Name = request.Name,
            AssetType = request.AssetType,
            LocationDescription = request.LocationDescription,
            SerialNumber = request.SerialNumber,
            InstallDate = request.InstallDate,
            WarrantyUntil = request.WarrantyUntil,
            VendorId = request.VendorId,
            Notes = request.Notes,
            CreatedBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        };

        _db.Assets.Add(asset);
        await _db.SaveChangesAsync();

        return Ok(new AssetDto
        {
            Id = asset.Id,
            BuildingId = asset.BuildingId,
            Name = asset.Name,
            AssetType = asset.AssetType,
            LocationDescription = asset.LocationDescription,
            SerialNumber = asset.SerialNumber,
            InstallDate = asset.InstallDate,
            WarrantyUntil = asset.WarrantyUntil,
            VendorId = asset.VendorId,
            Notes = asset.Notes
        });
    }

    [HttpGet("preventiveplans")]
    public async Task<ActionResult<List<PreventivePlanDto>>> GetPreventivePlans([FromQuery] int? assetId)
    {
        IQueryable<PreventivePlan> query = _db.PreventivePlans.Include(pp => pp.Asset);

        if (assetId.HasValue)
            query = query.Where(pp => pp.AssetId == assetId);

        var items = await query.Select(pp => new PreventivePlanDto
        {
            Id = pp.Id,
            AssetId = pp.AssetId,
            AssetName = pp.Asset.Name,
            Title = pp.Title,
            FrequencyType = pp.FrequencyType,
            Interval = pp.Interval,
            NextDueDate = pp.NextDueDate,
            ChecklistText = pp.ChecklistText
        }).ToListAsync();

        return Ok(items);
    }

    [HttpPost("preventiveplans")]
    public async Task<ActionResult<PreventivePlanDto>> CreatePreventivePlan([FromBody] CreatePreventivePlanRequest request)
    {
        var plan = new PreventivePlan
        {
            AssetId = request.AssetId,
            Title = request.Title,
            FrequencyType = request.FrequencyType,
            Interval = request.Interval,
            NextDueDate = request.NextDueDate,
            ChecklistText = request.ChecklistText,
            CreatedBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        };

        _db.PreventivePlans.Add(plan);
        await _db.SaveChangesAsync();

        return Ok(new PreventivePlanDto
        {
            Id = plan.Id,
            AssetId = plan.AssetId,
            Title = plan.Title,
            FrequencyType = plan.FrequencyType,
            Interval = plan.Interval,
            NextDueDate = plan.NextDueDate,
            ChecklistText = plan.ChecklistText
        });
    }

    [HttpPost("preventiveplans/generate-now")]
    public async Task<ActionResult<GenerateJobResponse>> GeneratePreventiveNow()
    {
        var (alreadyRan, periodKey, created) = await _jobService.GeneratePreventiveWorkOrdersAsync();
        return Ok(new GenerateJobResponse
        {
            AlreadyRan = alreadyRan,
            PeriodKey = periodKey,
            WorkOrdersCreated = created,
            Message = alreadyRan ? "Already ran for this period." : $"Created {created} preventive work orders."
        });
    }
}
