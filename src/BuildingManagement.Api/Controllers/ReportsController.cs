using BuildingManagement.Core.DTOs;
using BuildingManagement.Core.Entities;
using BuildingManagement.Core.Entities.Finance;
using BuildingManagement.Core.Enums;
using BuildingManagement.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace BuildingManagement.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
public class ReportsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ReportsController(AppDbContext db)
    {
        _db = db;
    }

    // ─── Collection Status (Who Paid / Who Has Not) ─────────

    [HttpGet("collection-status/{buildingId}")]
    public async Task<ActionResult<CollectionStatusReport>> CollectionStatus(
        int buildingId,
        [FromQuery] string? period,
        [FromQuery] bool includeNotGenerated = false)
    {
        period ??= DateTime.UtcNow.ToString("yyyy-MM");

        var building = await _db.Buildings.FindAsync(buildingId);
        if (building == null) return NotFound();

        var report = await BuildCollectionReport(building, period, includeNotGenerated);
        return Ok(report);
    }

    [HttpGet("collection-status/{buildingId}/unit/{unitId}")]
    public async Task<IActionResult> CollectionUnitDetail(
        int buildingId, int unitId, [FromQuery] string? period)
    {
        period ??= DateTime.UtcNow.ToString("yyyy-MM");

        var charge = await _db.UnitCharges
            .Include(uc => uc.Unit).ThenInclude(u => u.TenantUser)
            .Include(uc => uc.Allocations)
            .FirstOrDefaultAsync(uc => uc.UnitId == unitId
                && uc.Unit.BuildingId == buildingId
                && uc.Period == period);

        if (charge == null) return NotFound(new { message = $"No charge found for unit {unitId} in period {period}" });

        var payments = await _db.Payments
            .Where(p => p.UnitId == unitId && p.Status == PaymentStatus.Succeeded)
            .OrderByDescending(p => p.PaymentDateUtc)
            .Select(p => new { p.Id, p.Amount, p.PaymentDateUtc, p.ProviderReference, p.Status })
            .ToListAsync();

        return Ok(new
        {
            charge = new
            {
                charge.Id, charge.UnitId, charge.Period, charge.AmountDue, charge.DueDate, charge.Status,
                amountPaid = charge.Allocations.Sum(a => a.AllocatedAmount),
                allocations = charge.Allocations.Select(a => new { a.PaymentId, a.AllocatedAmount })
            },
            payments
        });
    }

    private async Task<CollectionStatusReport> BuildCollectionReport(Building building, string period, bool includeNotGenerated)
    {
        var buildingId = building.Id;
        var now = DateTime.UtcNow;
        // Asia/Jerusalem for overdue determination
        var israelNow = TimeZoneInfo.ConvertTimeFromUtc(now,
            TimeZoneInfo.FindSystemTimeZoneById("Israel Standard Time"));

        // All units in this building
        var allUnits = await _db.Units
            .Include(u => u.TenantUser)
            .Where(u => u.BuildingId == buildingId)
            .ToListAsync();

        // Charges for this period
        var charges = await _db.UnitCharges
            .Include(uc => uc.Allocations)
            .Where(uc => uc.Unit.BuildingId == buildingId && uc.Period == period)
            .ToListAsync();

        // Last payment date per unit
        var lastPaymentDates = await _db.Payments
            .Where(p => p.Unit.BuildingId == buildingId && p.Status == PaymentStatus.Succeeded)
            .GroupBy(p => p.UnitId)
            .Select(g => new { UnitId = g.Key, LastDate = g.Max(p => p.PaymentDateUtc) })
            .ToDictionaryAsync(x => x.UnitId, x => (DateTime?)x.LastDate);

        var chargeByUnit = charges.ToDictionary(c => c.UnitId);
        var rows = new List<CollectionRowDto>();

        foreach (var unit in allUnits)
        {
            if (chargeByUnit.TryGetValue(unit.Id, out var charge))
            {
                // Skip cancelled or zero charges
                if (charge.Status == UnitChargeStatus.Cancelled || charge.AmountDue == 0) continue;

                var paid = charge.Allocations.Sum(a => a.AllocatedAmount);
                var outstanding = charge.AmountDue - paid;

                string status;
                if (paid >= charge.AmountDue || charge.Status == UnitChargeStatus.Paid)
                    status = "Paid";
                else if (paid > 0)
                    status = "Partial";
                else if (charge.DueDate < israelNow.Date)
                    status = "Overdue";
                else
                    status = "Unpaid";

                rows.Add(new CollectionRowDto
                {
                    UnitId = unit.Id,
                    UnitNumber = unit.UnitNumber,
                    Floor = unit.Floor,
                    SizeSqm = unit.SizeSqm,
                    PayerDisplayName = unit.TenantUser?.FullName ?? unit.OwnerName ?? "—",
                    PayerPhone = unit.TenantUser?.Phone,
                    AmountDue = charge.AmountDue,
                    AmountPaid = paid,
                    Outstanding = outstanding > 0 ? outstanding : 0,
                    DueDate = charge.DueDate,
                    Status = status,
                    LastPaymentDateUtc = lastPaymentDates.GetValueOrDefault(unit.Id)
                });
            }
            else if (includeNotGenerated)
            {
                rows.Add(new CollectionRowDto
                {
                    UnitId = unit.Id,
                    UnitNumber = unit.UnitNumber,
                    Floor = unit.Floor,
                    SizeSqm = unit.SizeSqm,
                    PayerDisplayName = unit.TenantUser?.FullName ?? unit.OwnerName ?? "—",
                    PayerPhone = unit.TenantUser?.Phone,
                    AmountDue = 0,
                    AmountPaid = 0,
                    Outstanding = 0,
                    DueDate = null,
                    Status = "NotGenerated",
                    LastPaymentDateUtc = lastPaymentDates.GetValueOrDefault(unit.Id)
                });
            }
        }

        rows = rows.OrderBy(r => r.UnitNumber).ToList();
        var generated = rows.Where(r => r.Status != "NotGenerated").ToList();

        var summary = new CollectionSummaryDto
        {
            BuildingId = buildingId,
            BuildingName = building.Name,
            Period = period,
            TotalUnits = allUnits.Count,
            GeneratedCount = generated.Count,
            PaidCount = rows.Count(r => r.Status == "Paid"),
            PartialCount = rows.Count(r => r.Status == "Partial"),
            UnpaidCount = rows.Count(r => r.Status == "Unpaid"),
            OverdueCount = rows.Count(r => r.Status == "Overdue"),
            TotalDue = generated.Sum(r => r.AmountDue),
            TotalPaid = generated.Sum(r => r.AmountPaid),
            TotalOutstanding = generated.Sum(r => r.Outstanding),
            CollectionRatePercent = generated.Sum(r => r.AmountDue) > 0
                ? Math.Round(generated.Sum(r => r.AmountPaid) / generated.Sum(r => r.AmountDue) * 100, 1)
                : 0
        };

        return new CollectionStatusReport { Summary = summary, Rows = rows };
    }

    [HttpGet("aging/{buildingId}")]
    public async Task<ActionResult<AgingReport>> Aging(int buildingId)
    {
        var building = await _db.Buildings.FindAsync(buildingId);
        if (building == null) return NotFound();

        var now = DateTime.UtcNow;

        // Get all unpaid/partially paid charges for this building
        var charges = await _db.UnitCharges
            .Include(uc => uc.Unit).ThenInclude(u => u.TenantUser)
            .Include(uc => uc.Allocations)
            .Where(uc => uc.Unit.BuildingId == buildingId
                && (uc.Status == UnitChargeStatus.Pending
                    || uc.Status == UnitChargeStatus.PartiallyPaid
                    || uc.Status == UnitChargeStatus.Overdue))
            .ToListAsync();

        // Group by unit
        var grouped = charges.GroupBy(uc => uc.UnitId);

        var buckets = new List<AgingBucket>();
        foreach (var group in grouped)
        {
            var unit = group.First().Unit;
            var bucket = new AgingBucket
            {
                UnitId = unit.Id,
                UnitNumber = unit.UnitNumber,
                ResidentName = unit.TenantUser?.FullName ?? unit.OwnerName ?? "—"
            };

            decimal current = 0, d1 = 0, d31 = 0, d61 = 0, d90 = 0;

            foreach (var uc in group)
            {
                var paid = uc.Allocations.Sum(a => a.AllocatedAmount);
                var balance = uc.AmountDue - paid;
                if (balance <= 0) continue;

                var daysOverdue = (now - uc.DueDate).Days;
                if (daysOverdue <= 0) current += balance;
                else if (daysOverdue <= 30) d1 += balance;
                else if (daysOverdue <= 60) d31 += balance;
                else if (daysOverdue <= 90) d61 += balance;
                else d90 += balance;
            }

            buckets.Add(bucket with
            {
                Current = current,
                Days1to30 = d1,
                Days31to60 = d31,
                Days61to90 = d61,
                Days90Plus = d90,
                Total = current + d1 + d31 + d61 + d90
            });
        }

        return Ok(new AgingReport
        {
            BuildingId = buildingId,
            BuildingName = building.Name,
            Buckets = buckets.OrderBy(b => b.UnitNumber).ToList(),
            GrandTotal = buckets.Sum(b => b.Total)
        });
    }

    // ─── Income vs Expenses ────────────────────────────────

    [HttpGet("income-expenses/{buildingId}")]
    public async Task<ActionResult<IncomeExpensesReport>> IncomeExpenses(
        int buildingId, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var building = await _db.Buildings.FindAsync(buildingId);
        if (building == null) return NotFound();

        var fromDate = from ?? DateTime.UtcNow.AddMonths(-12);
        var toDate = to ?? DateTime.UtcNow;

        var entries = await _db.LedgerEntries
            .Where(le => le.BuildingId == buildingId
                && le.CreatedAtUtc >= fromDate
                && le.CreatedAtUtc <= toDate)
            .ToListAsync();

        // Income = Credits where EntryType == Payment
        var incomeEntries = entries.Where(e => e.EntryType == LedgerEntryType.Payment).ToList();
        var totalIncome = incomeEntries.Sum(e => e.Credit);

        // Expenses = Debits where EntryType == Expense
        var expenseEntries = entries.Where(e => e.EntryType == LedgerEntryType.Expense).ToList();
        var totalExpenses = expenseEntries.Sum(e => e.Debit);

        // Income by category
        var incomeByCategory = incomeEntries
            .GroupBy(e => e.Category ?? "HOAMonthlyFees")
            .Select(g => new CategoryAmount { Category = g.Key, Amount = g.Sum(e => e.Credit) })
            .OrderByDescending(c => c.Amount)
            .ToList();

        // Expenses by category
        var expensesByCategory = expenseEntries
            .GroupBy(e => e.Category ?? "Other")
            .Select(g => new CategoryAmount { Category = g.Key, Amount = g.Sum(e => e.Debit) })
            .OrderByDescending(c => c.Amount)
            .ToList();

        // Monthly breakdown
        var monthlyBreakdown = entries
            .GroupBy(e => e.CreatedAtUtc.ToString("yyyy-MM"))
            .OrderBy(g => g.Key)
            .Select(g => new MonthlyBreakdown
            {
                Month = g.Key,
                Income = g.Where(e => e.EntryType == LedgerEntryType.Payment).Sum(e => e.Credit),
                Expenses = g.Where(e => e.EntryType == LedgerEntryType.Expense).Sum(e => e.Debit),
                Net = g.Where(e => e.EntryType == LedgerEntryType.Payment).Sum(e => e.Credit)
                    - g.Where(e => e.EntryType == LedgerEntryType.Expense).Sum(e => e.Debit)
            })
            .ToList();

        return Ok(new IncomeExpensesReport
        {
            BuildingId = buildingId,
            BuildingName = building.Name,
            FromDate = fromDate,
            ToDate = toDate,
            TotalIncome = totalIncome,
            TotalExpenses = totalExpenses,
            NetBalance = totalIncome - totalExpenses,
            IncomeByCategory = incomeByCategory,
            ExpensesByCategory = expensesByCategory,
            MonthlyBreakdown = monthlyBreakdown
        });
    }

    [HttpGet("income-expenses/{buildingId}/csv")]
    public async Task<IActionResult> IncomeExpensesCsv(
        int buildingId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? lang)
    {
        var building = await _db.Buildings.FindAsync(buildingId);
        if (building == null) return NotFound();

        var fromDate = from ?? DateTime.UtcNow.AddMonths(-12);
        var toDate = to ?? DateTime.UtcNow;

        var entries = await _db.LedgerEntries
            .Where(le => le.BuildingId == buildingId
                && le.CreatedAtUtc >= fromDate
                && le.CreatedAtUtc <= toDate)
            .OrderBy(le => le.CreatedAtUtc)
            .ToListAsync();

        var h = GetIncomeExpenseHeaders(lang);
        var sb = new StringBuilder();
        sb.Append('\uFEFF'); // UTF-8 BOM for Excel
        sb.AppendLine($"{h["Date"]},{h["Type"]},{h["Category"]},{h["Description"]},{h["Debit"]},{h["Credit"]},{h["Balance"]}");

        decimal runningBalance = 0;
        foreach (var e in entries)
        {
            runningBalance += e.Credit - e.Debit;
            var type = e.EntryType.ToString();
            var category = e.Category ?? "—";
            var description = (e.Description ?? "").Replace("\"", "'");
            sb.AppendLine($"{e.CreatedAtUtc:yyyy-MM-dd},{type},\"{category}\",\"{description}\",{e.Debit:F2},{e.Credit:F2},{runningBalance:F2}");
        }

        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv",
            $"income-expenses-{buildingId}-{fromDate:yyyyMMdd}-{toDate:yyyyMMdd}.csv");
    }

    private static Dictionary<string, string> GetIncomeExpenseHeaders(string? lang)
    {
        lang = (lang ?? "he").ToLowerInvariant();
        return lang == "en"
            ? new() { ["Date"] = "Date", ["Type"] = "Type", ["Category"] = "Category", ["Description"] = "Description", ["Debit"] = "Debit", ["Credit"] = "Credit", ["Balance"] = "Balance" }
            : new() { ["Date"] = "תאריך", ["Type"] = "סוג", ["Category"] = "קטגוריה", ["Description"] = "תיאור", ["Debit"] = "חובה", ["Credit"] = "זכות", ["Balance"] = "יתרה" };
    }

    // ─── CSV Exports ────────────────────────────────────

    // Simple CSV header localization dictionary (NOT .resx, as per spec)
    private static readonly Dictionary<string, Dictionary<string, string>> CsvHeaders = new()
    {
        ["en"] = new()
        {
            ["Unit"] = "Unit", ["Floor"] = "Floor", ["Resident"] = "Resident",
            ["AmountDue"] = "Amount Due", ["AmountPaid"] = "Amount Paid", ["Balance"] = "Balance",
            ["Status"] = "Status", ["TotalExpected"] = "Total Expected", ["TotalCollected"] = "Total Collected",
            ["CollectionRate"] = "Collection Rate", ["Current"] = "Current",
            ["Days1to30"] = "1-30 Days", ["Days31to60"] = "31-60 Days", ["Days61to90"] = "61-90 Days",
            ["Days90Plus"] = "90+ Days", ["Total"] = "Total", ["GrandTotal"] = "Grand Total"
        },
        ["he"] = new()
        {
            ["Unit"] = "יחידה", ["Floor"] = "קומה", ["Resident"] = "דייר",
            ["AmountDue"] = "לתשלום", ["AmountPaid"] = "שולם", ["Balance"] = "יתרה",
            ["Status"] = "סטטוס", ["TotalExpected"] = "סה\"כ צפוי", ["TotalCollected"] = "סה\"כ נגבה",
            ["CollectionRate"] = "אחוז גבייה", ["Current"] = "שוטף",
            ["Days1to30"] = "1-30 יום", ["Days31to60"] = "31-60 יום", ["Days61to90"] = "61-90 יום",
            ["Days90Plus"] = "90+ יום", ["Total"] = "סה\"כ", ["GrandTotal"] = "סה\"כ חוב"
        }
    };

    private Dictionary<string, string> GetHeaders(string? lang)
    {
        lang = (lang ?? "he").ToLowerInvariant();
        return CsvHeaders.TryGetValue(lang, out var h) ? h : CsvHeaders["he"];
    }

    [HttpGet("collection-status/{buildingId}/csv")]
    public async Task<IActionResult> CollectionStatusCsv(
        int buildingId,
        [FromQuery] string? period,
        [FromQuery] bool includeNotGenerated = false,
        [FromQuery] string? lang = null)
    {
        var building = await _db.Buildings.FindAsync(buildingId);
        if (building == null) return NotFound();

        period ??= DateTime.UtcNow.ToString("yyyy-MM");
        var report = await BuildCollectionReport(building, period, includeNotGenerated);

        var h = GetHeaders(lang);
        var sb = new StringBuilder();
        sb.Append('\uFEFF');
        sb.AppendLine($"{h["Unit"]},{h["Floor"]},{h["Resident"]},{h["AmountDue"]},{h["AmountPaid"]},{h["Balance"]},{h["Status"]}");
        foreach (var r in report.Rows)
            sb.AppendLine($"\"{r.UnitNumber}\",{r.Floor},\"{r.PayerDisplayName}\",{r.AmountDue:F2},{r.AmountPaid:F2},{r.Outstanding:F2},{r.Status}");
        sb.AppendLine();
        sb.AppendLine($"{h["TotalExpected"]},{report.Summary.TotalDue:F2}");
        sb.AppendLine($"{h["TotalCollected"]},{report.Summary.TotalPaid:F2}");
        sb.AppendLine($"{h["CollectionRate"]},{report.Summary.CollectionRatePercent}%");

        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"collection-status-{buildingId}-{period}.csv");
    }

    [HttpGet("aging/{buildingId}/csv")]
    public async Task<IActionResult> AgingCsv(int buildingId, [FromQuery] string? lang)
    {
        var result = await Aging(buildingId);
        if (result.Result is not OkObjectResult ok || ok.Value is not AgingReport report)
            return NotFound();

        var h = GetHeaders(lang);
        var sb = new StringBuilder();
        sb.Append('\uFEFF');
        sb.AppendLine($"{h["Unit"]},{h["Resident"]},{h["Current"]},{h["Days1to30"]},{h["Days31to60"]},{h["Days61to90"]},{h["Days90Plus"]},{h["Total"]}");
        foreach (var b in report.Buckets)
            sb.AppendLine($"\"{b.UnitNumber}\",\"{b.ResidentName}\",{b.Current:F2},{b.Days1to30:F2},{b.Days31to60:F2},{b.Days61to90:F2},{b.Days90Plus:F2},{b.Total:F2}");
        sb.AppendLine();
        sb.AppendLine($"{h["GrandTotal"]},{report.GrandTotal:F2}");

        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"aging-report-{buildingId}.csv");
    }

    // ─── Dashboard Collection Summary ────────────────────

    [HttpGet("dashboard/collection")]
    public async Task<ActionResult<List<CollectionSummaryDto>>> DashboardCollection([FromQuery] string? period)
    {
        period ??= DateTime.UtcNow.ToString("yyyy-MM");

        var buildings = await _db.Buildings.ToListAsync();
        var summaries = new List<CollectionSummaryDto>();

        foreach (var building in buildings)
        {
            var report = await BuildCollectionReport(building, period, false);
            summaries.Add(report.Summary);
        }

        return Ok(summaries);
    }
}
