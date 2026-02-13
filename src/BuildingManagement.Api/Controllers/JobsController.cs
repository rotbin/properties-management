using BuildingManagement.Core.DTOs;
using BuildingManagement.Core.Enums;
using BuildingManagement.Infrastructure.Data;
using BuildingManagement.Infrastructure.Jobs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BuildingManagement.Api.Controllers;

[ApiController]
[Route("api/jobs")]
[Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
public class JobsController : ControllerBase
{
    private readonly MaintenanceJobService _jobService;
    private readonly AppDbContext _db;

    public JobsController(MaintenanceJobService jobService, AppDbContext db)
    {
        _jobService = jobService;
        _db = db;
    }

    [HttpPost("generate-preventive")]
    public async Task<ActionResult<GenerateJobResponse>> GeneratePreventive()
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

    [HttpPost("generate-cleaning-week")]
    public async Task<ActionResult<GenerateJobResponse>> GenerateCleaningWeek()
    {
        var (alreadyRan, periodKey, created) = await _jobService.GenerateCleaningWorkOrdersAsync();
        return Ok(new GenerateJobResponse
        {
            AlreadyRan = alreadyRan,
            PeriodKey = periodKey,
            WorkOrdersCreated = created,
            Message = alreadyRan ? "Already ran for this period." : $"Created {created} cleaning work orders."
        });
    }

    [HttpGet("logs")]
    public async Task<ActionResult<List<JobRunLogDto>>> GetLogs()
    {
        var logs = await _db.JobRunLogs
            .OrderByDescending(j => j.RanAtUtc)
            .Take(100)
            .Select(j => new JobRunLogDto
            {
                Id = j.Id,
                JobName = j.JobName,
                PeriodKey = j.PeriodKey,
                RanAtUtc = j.RanAtUtc
            }).ToListAsync();

        return Ok(logs);
    }
}
