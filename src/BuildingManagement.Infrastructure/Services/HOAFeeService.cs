using BuildingManagement.Core.Entities;
using BuildingManagement.Core.Entities.Finance;
using BuildingManagement.Core.Enums;
using BuildingManagement.Core.Interfaces;
using BuildingManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BuildingManagement.Infrastructure.Services;

public class HOAFeeService : IHOAFeeService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<HOAFeeService> _logger;

    public HOAFeeService(IServiceProvider serviceProvider, ILogger<HOAFeeService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<GenerateChargesResult> GenerateMonthlyChargesAsync(int buildingId, string period, CancellationToken ct = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var jobName = $"HOACharges-B{buildingId}";

        // Idempotency check via JobRunLog
        if (await db.JobRunLogs.AnyAsync(j => j.JobName == jobName && j.PeriodKey == period, ct))
        {
            _logger.LogInformation("HOA charges for building {BuildingId} period {Period} already generated", buildingId, period);
            return new GenerateChargesResult(true, period, 0, "Already generated for this period.");
        }

        // Get active fee plan for building
        var plan = await db.HOAFeePlans
            .Where(p => p.BuildingId == buildingId && p.IsActive && !p.IsDeleted)
            .OrderByDescending(p => p.EffectiveFrom)
            .FirstOrDefaultAsync(ct);

        if (plan == null)
            return new GenerateChargesResult(false, period, 0, "No active HOA fee plan found for this building.");

        // Get all units in building
        var units = await db.Units
            .Where(u => u.BuildingId == buildingId)
            .ToListAsync(ct);

        // Parse period to determine due date (1st of next month)
        var year = int.Parse(period[..4]);
        var month = int.Parse(period[5..]);
        var dueDate = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1).AddDays(-1); // last day of month

        var created = 0;
        foreach (var unit in units)
        {
            var amount = CalculateUnitAmount(unit, plan);
            if (amount <= 0) continue;

            // Check if charge already exists (safety, in addition to JobRunLog)
            var exists = await db.UnitCharges.AnyAsync(
                uc => uc.UnitId == unit.Id && uc.HOAFeePlanId == plan.Id && uc.Period == period, ct);
            if (exists) continue;

            var charge = new UnitCharge
            {
                UnitId = unit.Id,
                HOAFeePlanId = plan.Id,
                Period = period,
                AmountDue = amount,
                DueDate = dueDate,
                Status = UnitChargeStatus.Pending
            };
            db.UnitCharges.Add(charge);

            // Create ledger entry
            // Get current balance for unit
            var lastBalance = await db.LedgerEntries
                .Where(le => le.UnitId == unit.Id)
                .OrderByDescending(le => le.Id)
                .Select(le => (decimal?)le.BalanceAfter)
                .FirstOrDefaultAsync(ct) ?? 0m;

            db.LedgerEntries.Add(new LedgerEntry
            {
                BuildingId = buildingId,
                UnitId = unit.Id,
                EntryType = LedgerEntryType.Charge,
                Debit = amount,
                Credit = 0,
                BalanceAfter = lastBalance + amount
            });

            created++;
        }

        db.JobRunLogs.Add(new JobRunLog { JobName = jobName, PeriodKey = period });
        await db.SaveChangesAsync(ct);

        _logger.LogInformation("Generated {Count} HOA charges for building {BuildingId} period {Period}", created, buildingId, period);
        return new GenerateChargesResult(false, period, created, $"Generated {created} charges for {period}.");
    }

    public static decimal CalculateUnitAmount(Unit unit, HOAFeePlan plan)
    {
        return plan.CalculationMethod switch
        {
            HOACalculationMethod.BySqm => (unit.SizeSqm ?? 0) * (plan.AmountPerSqm ?? 0),
            HOACalculationMethod.FixedPerUnit => plan.FixedAmountPerUnit ?? 0,
            HOACalculationMethod.ManualPerUnit => 0, // Manual: manager sets individually
            _ => 0
        };
    }
}
