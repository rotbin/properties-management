using System.Globalization;
using BuildingManagement.Core.Entities;
using BuildingManagement.Core.Enums;
using BuildingManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BuildingManagement.Infrastructure.Jobs;

/// <summary>
/// Background service that can optionally auto-run generators.
/// By default disabled; generators are triggered via manager endpoints.
/// </summary>
public class MaintenanceJobService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MaintenanceJobService> _logger;
    private readonly bool _autoRunEnabled;

    public MaintenanceJobService(IServiceProvider serviceProvider, ILogger<MaintenanceJobService> logger, IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _autoRunEnabled = configuration.GetValue<bool>("Jobs:AutoRunEnabled");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_autoRunEnabled)
        {
            _logger.LogInformation("Auto-run maintenance jobs disabled. Use API endpoints to trigger manually.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await GenerateCleaningWorkOrdersAsync();
                await GeneratePreventiveWorkOrdersAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running maintenance jobs");
            }

            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
        }
    }

    public async Task<(bool alreadyRan, string periodKey, int created)> GenerateCleaningWorkOrdersAsync(int? buildingId = null)
    {
        var weekKey = GetCurrentWeekKey();
        var jobName = buildingId.HasValue ? $"CleaningWeekly-B{buildingId}" : "CleaningWeekly";

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Check idempotency
        if (await db.JobRunLogs.AnyAsync(j => j.JobName == jobName && j.PeriodKey == weekKey))
        {
            _logger.LogInformation("Cleaning job {JobName} already ran for {PeriodKey}", jobName, weekKey);
            return (true, weekKey, 0);
        }

        var query = db.CleaningPlans
            .Include(cp => cp.Building)
            .Include(cp => cp.CleaningVendor)
            .AsQueryable();

        if (buildingId.HasValue)
            query = query.Where(cp => cp.BuildingId == buildingId.Value);

        var plans = await query.ToListAsync();
        var created = 0;

        foreach (var plan in plans)
        {
            var areas = new (string area, int count)[]
            {
                ("Stairwells", plan.StairwellsPerWeek),
                ("Parking", plan.ParkingPerWeek),
                ("Corridor & Lobby", plan.CorridorLobbyPerWeek),
                ("Garbage Room", plan.GarbageRoomPerWeek),
            };

            foreach (var (area, count) in areas)
            {
                for (int i = 0; i < count; i++)
                {
                    db.WorkOrders.Add(new WorkOrder
                    {
                        BuildingId = plan.BuildingId,
                        VendorId = plan.CleaningVendorId,
                        Title = $"Cleaning - {area} - {plan.Building.Name} - Week {weekKey}",
                        Description = $"Scheduled cleaning for {area}. Session {i + 1} of {count} this week.",
                        Status = WorkOrderStatus.Assigned,
                        ScheduledFor = GetNextWeekday(i),
                        CreatedBy = "System"
                    });
                    created++;
                }
            }
        }

        db.JobRunLogs.Add(new JobRunLog { JobName = jobName, PeriodKey = weekKey });
        await db.SaveChangesAsync();

        _logger.LogInformation("Generated {Count} cleaning work orders for period {PeriodKey}", created, weekKey);
        return (false, weekKey, created);
    }

    public async Task<(bool alreadyRan, string periodKey, int created)> GeneratePreventiveWorkOrdersAsync()
    {
        var dateKey = DateTime.UtcNow.ToString("yyyy-MM-dd");
        const string jobName = "PreventiveMaintenance";

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (await db.JobRunLogs.AnyAsync(j => j.JobName == jobName && j.PeriodKey == dateKey))
        {
            _logger.LogInformation("Preventive job already ran for {PeriodKey}", dateKey);
            return (true, dateKey, 0);
        }

        var duePlans = await db.PreventivePlans
            .Include(pp => pp.Asset)
            .ThenInclude(a => a.Building)
            .Where(pp => pp.NextDueDate <= DateTime.UtcNow)
            .ToListAsync();

        var created = 0;
        foreach (var plan in duePlans)
        {
            db.WorkOrders.Add(new WorkOrder
            {
                BuildingId = plan.Asset.BuildingId,
                Title = $"Preventive: {plan.Title} - {plan.Asset.Name}",
                Description = $"Checklist:\n{plan.ChecklistText ?? "N/A"}",
                Status = WorkOrderStatus.Draft,
                VendorId = plan.Asset.VendorId,
                CreatedBy = "System"
            });
            created++;

            // Advance next due date
            plan.NextDueDate = plan.FrequencyType switch
            {
                FrequencyType.Daily => plan.NextDueDate.AddDays(plan.Interval),
                FrequencyType.Weekly => plan.NextDueDate.AddDays(7 * plan.Interval),
                FrequencyType.BiWeekly => plan.NextDueDate.AddDays(14 * plan.Interval),
                FrequencyType.Monthly => plan.NextDueDate.AddMonths(plan.Interval),
                FrequencyType.Quarterly => plan.NextDueDate.AddMonths(3 * plan.Interval),
                FrequencyType.Yearly => plan.NextDueDate.AddYears(plan.Interval),
                _ => plan.NextDueDate.AddMonths(plan.Interval)
            };
        }

        db.JobRunLogs.Add(new JobRunLog { JobName = jobName, PeriodKey = dateKey });
        await db.SaveChangesAsync();

        _logger.LogInformation("Generated {Count} preventive work orders for {PeriodKey}", created, dateKey);
        return (false, dateKey, created);
    }

    private static string GetCurrentWeekKey()
    {
        var now = DateTime.UtcNow;
        var cal = CultureInfo.InvariantCulture.Calendar;
        var week = cal.GetWeekOfYear(now, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        return $"{now.Year}-W{week:D2}";
    }

    private static DateTime GetNextWeekday(int dayOffset)
    {
        var today = DateTime.UtcNow.Date;
        var monday = today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday);
        if (monday < today) monday = monday.AddDays(7);
        return monday.AddDays(dayOffset);
    }
}
