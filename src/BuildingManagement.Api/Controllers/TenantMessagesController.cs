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
[Route("api/tenant-messages")]
[Authorize]
public class TenantMessagesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<TenantMessagesController> _logger;

    public TenantMessagesController(AppDbContext db, ILogger<TenantMessagesController> logger)
    {
        _db = db;
        _logger = logger;
    }

    // ─── Manager endpoints ────────────────────────────────

    [HttpGet("tenant/{tenantProfileId}")]
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<ActionResult<List<TenantMessageDto>>> GetMessagesForTenant(int tenantProfileId)
    {
        var messages = await _db.TenantMessages
            .Where(m => m.TenantProfileId == tenantProfileId)
            .OrderByDescending(m => m.CreatedAtUtc)
            .Select(m => new TenantMessageDto
            {
                Id = m.Id,
                TenantProfileId = m.TenantProfileId,
                TenantName = m.TenantProfile.FullName,
                SentByUserId = m.SentByUserId,
                SentByName = m.SentByUser != null ? m.SentByUser.FullName : "AI Agent",
                Subject = m.Subject,
                Body = m.Body,
                MessageType = m.MessageType,
                PayerCategory = m.PayerCategory,
                IsRead = m.IsRead,
                CreatedAtUtc = m.CreatedAtUtc,
                ReadAtUtc = m.ReadAtUtc
            })
            .ToListAsync();

        return Ok(messages);
    }

    [HttpPost("tenant/{tenantProfileId}/send")]
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<ActionResult<TenantMessageDto>> SendMessage(int tenantProfileId, [FromBody] SendTenantMessageRequest req)
    {
        var tp = await _db.TenantProfiles.FirstOrDefaultAsync(t => t.Id == tenantProfileId);
        if (tp == null) return NotFound();

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
        var userName = User.FindFirst("fullName")?.Value ?? "Manager";

        var msg = new TenantMessage
        {
            TenantProfileId = tenantProfileId,
            SentByUserId = userId,
            Subject = req.Subject,
            Body = req.Body,
            MessageType = "Manual"
        };
        _db.TenantMessages.Add(msg);
        await _db.SaveChangesAsync();

        return Ok(new TenantMessageDto
        {
            Id = msg.Id,
            TenantProfileId = msg.TenantProfileId,
            TenantName = tp.FullName,
            SentByUserId = userId,
            SentByName = userName,
            Subject = msg.Subject,
            Body = msg.Body,
            MessageType = msg.MessageType,
            IsRead = false,
            CreatedAtUtc = msg.CreatedAtUtc
        });
    }

    // ─── Payment analysis & AI reminders ──────────────────

    [HttpGet("payment-analysis/{buildingId}")]
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<ActionResult<List<PaymentAnalysisDto>>> GetPaymentAnalysis(int buildingId)
    {
        var analyses = await AnalyzeTenantsInBuilding(buildingId);
        return Ok(analyses);
    }

    [HttpPost("send-payment-reminders")]
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<ActionResult<object>> SendPaymentReminders([FromBody] SendPaymentRemindersRequest req)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
        var analyses = await AnalyzeTenantsInBuilding(req.BuildingId);

        var tenantsWithDebt = analyses.Where(a => a.Outstanding > 0).ToList();
        if (tenantsWithDebt.Count == 0)
            return Ok(new { sent = 0, message = "No tenants with outstanding balance." });

        var building = await _db.Buildings.FindAsync(req.BuildingId);
        var paymentUrl = $"/my-charges";
        int sent = 0;

        foreach (var analysis in tenantsWithDebt)
        {
            var (subject, body) = GeneratePaymentMessage(analysis, building?.Name ?? "the building", paymentUrl);

            var msg = new TenantMessage
            {
                TenantProfileId = analysis.TenantProfileId,
                SentByUserId = null,
                Subject = subject,
                Body = body,
                MessageType = analysis.PayerCategory == "ChronicallyLate" ? "Warning" : "PaymentReminder",
                PayerCategory = analysis.PayerCategory
            };
            _db.TenantMessages.Add(msg);
            sent++;
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("Sent {Count} payment reminders for building {BuildingId}", sent, req.BuildingId);

        return Ok(new { sent, message = $"Sent {sent} payment reminder(s)." });
    }

    // ─── Tenant endpoints ─────────────────────────────────

    [HttpGet("my-messages")]
    [Authorize(Roles = AppRoles.Tenant)]
    public async Task<ActionResult<List<TenantMessageDto>>> GetMyMessages()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
        var tenantProfile = await _db.TenantProfiles
            .FirstOrDefaultAsync(t => t.UserId == userId && t.IsActive && !t.IsDeleted);
        if (tenantProfile == null) return Ok(new List<TenantMessageDto>());

        var messages = await _db.TenantMessages
            .Where(m => m.TenantProfileId == tenantProfile.Id)
            .OrderByDescending(m => m.CreatedAtUtc)
            .Select(m => new TenantMessageDto
            {
                Id = m.Id,
                TenantProfileId = m.TenantProfileId,
                TenantName = m.TenantProfile.FullName,
                SentByUserId = m.SentByUserId,
                SentByName = m.SentByUser != null ? m.SentByUser.FullName : "AI Agent",
                Subject = m.Subject,
                Body = m.Body,
                MessageType = m.MessageType,
                PayerCategory = m.PayerCategory,
                IsRead = m.IsRead,
                CreatedAtUtc = m.CreatedAtUtc,
                ReadAtUtc = m.ReadAtUtc
            })
            .ToListAsync();

        return Ok(messages);
    }

    [HttpGet("my-unread-count")]
    [Authorize(Roles = AppRoles.Tenant)]
    public async Task<ActionResult<int>> GetMyUnreadCount()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
        var tenantProfile = await _db.TenantProfiles
            .FirstOrDefaultAsync(t => t.UserId == userId && t.IsActive && !t.IsDeleted);
        if (tenantProfile == null) return Ok(0);

        var count = await _db.TenantMessages
            .CountAsync(m => m.TenantProfileId == tenantProfile.Id && !m.IsRead);
        return Ok(count);
    }

    [HttpPost("{id}/mark-read")]
    [Authorize(Roles = AppRoles.Tenant)]
    public async Task<IActionResult> MarkRead(int id)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
        var tenantProfile = await _db.TenantProfiles
            .FirstOrDefaultAsync(t => t.UserId == userId && t.IsActive && !t.IsDeleted);
        if (tenantProfile == null) return NotFound();

        var msg = await _db.TenantMessages
            .FirstOrDefaultAsync(m => m.Id == id && m.TenantProfileId == tenantProfile.Id);
        if (msg == null) return NotFound();

        if (!msg.IsRead)
        {
            msg.IsRead = true;
            msg.ReadAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
        return Ok();
    }

    [HttpPost("mark-all-read")]
    [Authorize(Roles = AppRoles.Tenant)]
    public async Task<IActionResult> MarkAllRead()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
        var tenantProfile = await _db.TenantProfiles
            .FirstOrDefaultAsync(t => t.UserId == userId && t.IsActive && !t.IsDeleted);
        if (tenantProfile == null) return NotFound();

        var unread = await _db.TenantMessages
            .Where(m => m.TenantProfileId == tenantProfile.Id && !m.IsRead)
            .ToListAsync();

        foreach (var msg in unread)
        {
            msg.IsRead = true;
            msg.ReadAtUtc = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync();
        return Ok();
    }

    // ─── Payment analysis helpers ─────────────────────────

    private async Task<List<PaymentAnalysisDto>> AnalyzeTenantsInBuilding(int buildingId)
    {
        var tenants = await _db.TenantProfiles
            .Include(t => t.Unit).ThenInclude(u => u.Building)
            .Where(t => t.IsActive && !t.IsDeleted && t.Unit.BuildingId == buildingId)
            .ToListAsync();

        var unitIds = tenants.Select(t => t.UnitId).Distinct().ToList();

        var charges = await _db.UnitCharges
            .Where(c => unitIds.Contains(c.UnitId))
            .ToListAsync();

        var payments = await _db.Payments
            .Include(p => p.Allocations)
            .Where(p => unitIds.Contains(p.UnitId) && p.Status == PaymentStatus.Succeeded)
            .ToListAsync();

        var standingOrders = await _db.StandingOrders
            .Where(so => unitIds.Contains(so.UnitId) && so.Status == StandingOrderStatus.Active)
            .Select(so => so.UnitId)
            .Distinct()
            .ToListAsync();

        var results = new List<PaymentAnalysisDto>();

        foreach (var tenant in tenants)
        {
            var unitCharges = charges.Where(c => c.UnitId == tenant.UnitId).ToList();
            var unitPayments = payments.Where(p => p.UnitId == tenant.UnitId).ToList();

            var totalDue = unitCharges.Sum(c => c.AmountDue);
            var totalPaid = unitPayments.Sum(p => p.Amount);
            var outstanding = Math.Max(0, totalDue - totalPaid);
            var overdueCount = unitCharges.Count(c => c.Status == UnitChargeStatus.Overdue);
            var totalChargeCount = unitCharges.Count;

            // Count late payments: payments made after the charge due date
            var latePayments = 0;
            foreach (var charge in unitCharges.Where(c => c.Status != UnitChargeStatus.Cancelled))
            {
                var allocations = unitPayments
                    .SelectMany(p => p.Allocations.Where(a => a.UnitChargeId == charge.Id).Select(a => new { p.PaymentDateUtc }))
                    .ToList();
                if (allocations.Any(a => a.PaymentDateUtc > charge.DueDate.AddDays(7)))
                    latePayments++;
            }

            var paidCharges = unitCharges.Count(c => c.Status == UnitChargeStatus.Paid || c.Status == UnitChargeStatus.PartiallyPaid);
            var onTimeRate = totalChargeCount > 0 ? (double)(totalChargeCount - latePayments) / totalChargeCount * 100 : 100;

            var category = CategorizePayerBehavior(onTimeRate, overdueCount, outstanding, totalDue);

            results.Add(new PaymentAnalysisDto
            {
                TenantProfileId = tenant.Id,
                TenantName = tenant.FullName,
                UnitNumber = tenant.Unit?.UnitNumber,
                BuildingName = tenant.Unit?.Building?.Name,
                PayerCategory = category,
                TotalDue = totalDue,
                TotalPaid = totalPaid,
                Outstanding = outstanding,
                OverdueCount = overdueCount,
                TotalCharges = totalChargeCount,
                LatePayments = latePayments,
                OnTimeRate = Math.Round(onTimeRate, 1),
                HasStandingOrder = standingOrders.Contains(tenant.UnitId)
            });
        }

        return results.OrderByDescending(r => r.Outstanding).ToList();
    }

    private static string CategorizePayerBehavior(double onTimeRate, int overdueCount, decimal outstanding, decimal totalDue)
    {
        if (outstanding == 0 && overdueCount == 0 && onTimeRate >= 90) return "GoodPayer";
        if (overdueCount >= 3 || onTimeRate < 50) return "ChronicallyLate";
        if (overdueCount >= 1 || onTimeRate < 80) return "OccasionallyLate";
        return "GoodPayer";
    }

    private static (string Subject, string Body) GeneratePaymentMessage(
        PaymentAnalysisDto analysis, string buildingName, string paymentUrl)
    {
        return analysis.PayerCategory switch
        {
            "GoodPayer" => (
                "Friendly payment reminder",
                $"Hi {analysis.TenantName},\n\n" +
                $"We appreciate you being a great member of {buildingName}! " +
                $"Just a quick heads-up: you have an outstanding balance of ₪{analysis.Outstanding:N0}.\n\n" +
                $"You can pay easily through the app under \"My Charges\" ({paymentUrl}).\n\n" +
                "Thank you for always being on time with your payments!"
            ),
            "OccasionallyLate" => (
                "Payment reminder — balance due",
                $"Hi {analysis.TenantName},\n\n" +
                $"This is a reminder that you have an outstanding balance of ₪{analysis.Outstanding:N0} " +
                $"for your unit at {buildingName}. " +
                $"You currently have {analysis.OverdueCount} overdue charge(s).\n\n" +
                $"Please make a payment through \"My Charges\" in the app ({paymentUrl}).\n\n" +
                (analysis.HasStandingOrder ? "" :
                "💡 **Tip:** To avoid future delays, you can set up a Standing Order (automatic monthly payment) " +
                "through the \"My Charges\" page. This way your payments will always be on time!\n\n") +
                "If you have any questions, please contact the building management."
            ),
            "ChronicallyLate" => (
                "⚠️ Urgent: Overdue payment notice",
                $"Dear {analysis.TenantName},\n\n" +
                $"**This is an important notice regarding your overdue balance.**\n\n" +
                $"Your outstanding balance is ₪{analysis.Outstanding:N0} for your unit at {buildingName}. " +
                $"You have {analysis.OverdueCount} overdue charge(s) and your on-time payment rate is {analysis.OnTimeRate:N0}%.\n\n" +
                $"Please settle your balance as soon as possible through \"My Charges\" in the app ({paymentUrl}).\n\n" +
                (analysis.HasStandingOrder ? "" :
                "🔔 **We strongly recommend setting up a Standing Order** (automatic monthly payment) " +
                "to ensure timely payments and avoid further collection actions. " +
                "You can do this from \"My Charges\" → \"Standing Orders\" tab.\n\n") +
                "Continued non-payment may result in additional fees or further action by the building committee.\n\n" +
                "If you're experiencing financial difficulties, please reach out to the building management so we can discuss options."
            ),
            _ => (
                "Payment reminder",
                $"Hi {analysis.TenantName},\n\nYou have an outstanding balance of ₪{analysis.Outstanding:N0}. " +
                $"Please pay through the app ({paymentUrl})."
            )
        };
    }
}
