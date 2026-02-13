using BuildingManagement.Core.DTOs;
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

    [HttpGet("collection-status/{buildingId}")]
    public async Task<ActionResult<CollectionStatusReport>> CollectionStatus(int buildingId, [FromQuery] string? period)
    {
        period ??= DateTime.UtcNow.ToString("yyyy-MM");

        var building = await _db.Buildings.FindAsync(buildingId);
        if (building == null) return NotFound();

        var charges = await _db.UnitCharges
            .Include(uc => uc.Unit).ThenInclude(u => u.TenantUser)
            .Include(uc => uc.Allocations)
            .Where(uc => uc.Unit.BuildingId == buildingId && uc.Period == period)
            .ToListAsync();

        var rows = charges.Select(uc =>
        {
            var paid = uc.Allocations.Sum(a => a.AllocatedAmount);
            return new CollectionStatusRow
            {
                UnitId = uc.UnitId,
                UnitNumber = uc.Unit.UnitNumber,
                Floor = uc.Unit.Floor,
                ResidentName = uc.Unit.TenantUser?.FullName ?? uc.Unit.OwnerName ?? "—",
                AmountDue = uc.AmountDue,
                AmountPaid = paid,
                Balance = uc.AmountDue - paid,
                Status = uc.Status.ToString()
            };
        }).OrderBy(r => r.UnitNumber).ToList();

        var totalExpected = rows.Sum(r => r.AmountDue);
        var totalCollected = rows.Sum(r => r.AmountPaid);

        return Ok(new CollectionStatusReport
        {
            BuildingId = buildingId,
            BuildingName = building.Name,
            Period = period,
            TotalExpected = totalExpected,
            TotalCollected = totalCollected,
            CollectionRate = totalExpected > 0 ? Math.Round(totalCollected / totalExpected * 100, 1) : 0,
            Rows = rows
        });
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
    public async Task<IActionResult> CollectionStatusCsv(int buildingId, [FromQuery] string? period, [FromQuery] string? lang)
    {
        var result = await CollectionStatus(buildingId, period);
        if (result.Result is not OkObjectResult ok || ok.Value is not CollectionStatusReport report)
            return NotFound();

        var h = GetHeaders(lang);
        var sb = new StringBuilder();
        // UTF-8 BOM for proper Hebrew display in Excel
        sb.Append('\uFEFF');
        sb.AppendLine($"{h["Unit"]},{h["Floor"]},{h["Resident"]},{h["AmountDue"]},{h["AmountPaid"]},{h["Balance"]},{h["Status"]}");
        foreach (var r in report.Rows)
            sb.AppendLine($"\"{r.UnitNumber}\",{r.Floor},\"{r.ResidentName}\",{r.AmountDue:F2},{r.AmountPaid:F2},{r.Balance:F2},{r.Status}");
        sb.AppendLine();
        sb.AppendLine($"{h["TotalExpected"]},{report.TotalExpected:F2}");
        sb.AppendLine($"{h["TotalCollected"]},{report.TotalCollected:F2}");
        sb.AppendLine($"{h["CollectionRate"]},{report.CollectionRate}%");

        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"collection-status-{buildingId}-{period ?? "current"}.csv");
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
}
