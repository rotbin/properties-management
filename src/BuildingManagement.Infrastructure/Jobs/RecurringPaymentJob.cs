using BuildingManagement.Core.Entities;
using BuildingManagement.Core.Entities.Finance;
using BuildingManagement.Core.Enums;
using BuildingManagement.Core.Interfaces;
using BuildingManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BuildingManagement.Infrastructure.Jobs;

/// <summary>
/// Background service that attempts recurring charges for units
/// with a default payment method and outstanding charges.
/// Runs daily. Retries failures up to 3 times across consecutive runs.
/// </summary>
public class RecurringPaymentJob : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RecurringPaymentJob> _logger;
    private readonly bool _autoRunEnabled;
    private const int MaxRetries = 3;

    public RecurringPaymentJob(IServiceProvider serviceProvider, ILogger<RecurringPaymentJob> logger, IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _autoRunEnabled = configuration.GetValue<bool>("Jobs:RecurringPaymentsEnabled");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_autoRunEnabled)
        {
            _logger.LogInformation("Recurring payment job disabled. Use API endpoints to trigger manually.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ProcessRecurringPaymentsAsync(stoppingToken); }
            catch (Exception ex) { _logger.LogError(ex, "Error in recurring payment job"); }
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }

    public async Task<int> ProcessRecurringPaymentsAsync(CancellationToken ct = default)
    {
        var periodKey = DateTime.UtcNow.ToString("yyyy-MM");
        var jobName = $"RecurringPayments-{periodKey}";

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var gatewayFactory = scope.ServiceProvider.GetRequiredService<IPaymentGatewayFactory>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        var todayKey = DateTime.UtcNow.ToString("yyyy-MM-dd");
        if (await db.JobRunLogs.AnyAsync(j => j.JobName == jobName && j.PeriodKey == todayKey, ct))
        {
            _logger.LogInformation("Recurring payments already processed for {Date}", todayKey);
            return 0;
        }

        var pendingCharges = await db.UnitCharges
            .Include(uc => uc.Unit).ThenInclude(u => u.TenantUser)
            .Where(uc => uc.Status == UnitChargeStatus.Pending || uc.Status == UnitChargeStatus.Overdue)
            .ToListAsync(ct);

        var processed = 0;

        foreach (var charge in pendingCharges)
        {
            var tenantUserId = charge.Unit?.TenantUserId;
            if (tenantUserId == null) continue;

            var paymentMethod = await db.PaymentMethods
                .Where(pm => pm.UserId == tenantUserId && pm.IsDefault && pm.IsActive)
                .FirstOrDefaultAsync(ct);
            if (paymentMethod == null) continue;

            // Check retry count for this charge
            var failCount = await db.Payments
                .Where(p => p.UnitId == charge.UnitId && p.Status == PaymentStatus.Failed)
                .CountAsync(ct);
            if (failCount >= MaxRetries)
            {
                _logger.LogWarning("Max retries ({Max}) reached for unit {UnitId} charge {ChargeId}, skipping",
                    MaxRetries, charge.UnitId, charge.Id);
                continue;
            }

            var totalPaid = await db.PaymentAllocations
                .Where(pa => pa.UnitChargeId == charge.Id)
                .SumAsync(pa => pa.AllocatedAmount, ct);
            var remaining = charge.AmountDue - totalPaid;
            if (remaining <= 0) continue;

            // Resolve gateway for the building
            var gateway = await gatewayFactory.GetGatewayAsync(charge.Unit?.BuildingId, ct);

            var result = await gateway.ChargeTokenAsync(new ChargeTokenRequest(
                BuildingId: charge.Unit!.BuildingId,
                Token: paymentMethod.Token,
                Amount: remaining,
                Currency: "ILS",
                Description: $"HOA Recurring - Unit {charge.Unit.UnitNumber} - {charge.Period}",
                IdempotencyKey: $"recurring-{charge.Id}-{todayKey}"),
                ct);

            var payment = new Payment
            {
                UnitId = charge.UnitId,
                UserId = tenantUserId,
                Amount = remaining,
                PaymentMethodId = paymentMethod.Id,
                ProviderReference = result.ProviderReference,
                Status = result.Success ? PaymentStatus.Succeeded : PaymentStatus.Failed
            };
            db.Payments.Add(payment);
            await db.SaveChangesAsync(ct);

            if (result.Success)
            {
                db.PaymentAllocations.Add(new PaymentAllocation
                {
                    PaymentId = payment.Id,
                    UnitChargeId = charge.Id,
                    AllocatedAmount = remaining
                });
                charge.Status = UnitChargeStatus.Paid;

                var lastBalance = await db.LedgerEntries
                    .Where(le => le.UnitId == charge.UnitId)
                    .OrderByDescending(le => le.Id)
                    .Select(le => (decimal?)le.BalanceAfter)
                    .FirstOrDefaultAsync(ct) ?? 0m;

                db.LedgerEntries.Add(new LedgerEntry
                {
                    BuildingId = charge.Unit.BuildingId,
                    UnitId = charge.UnitId,
                    EntryType = LedgerEntryType.Payment,
                    Category = "HOAMonthlyFees",
                    Description = $"Recurring payment for charge #{charge.Id}",
                    ReferenceId = payment.Id,
                    Debit = 0,
                    Credit = remaining,
                    BalanceAfter = lastBalance - remaining
                });
                await db.SaveChangesAsync(ct);

                if (charge.Unit.TenantUser?.Email != null)
                {
                    await emailService.SendEmailAsync(
                        charge.Unit.TenantUser.Email,
                        $"Payment Received - {charge.Period}",
                        $"Your HOA payment of {remaining:N2} ILS for {charge.Period} has been processed successfully.",
                        ct);
                }
                processed++;
            }
            else
            {
                _logger.LogWarning("Recurring charge failed for unit {UnitId}, charge {ChargeId} (attempt {Attempt}/{Max}): {Error}",
                    charge.UnitId, charge.Id, failCount + 1, MaxRetries, result.Error);
            }
        }

        db.JobRunLogs.Add(new JobRunLog { JobName = jobName, PeriodKey = todayKey });
        await db.SaveChangesAsync(ct);

        _logger.LogInformation("Processed {Count} recurring payments for {Date}", processed, todayKey);
        return processed;
    }
}
