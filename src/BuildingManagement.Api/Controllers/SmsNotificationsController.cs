using BuildingManagement.Core.DTOs;
using BuildingManagement.Core.Entities;
using BuildingManagement.Core.Entities.Notifications;
using BuildingManagement.Core.Enums;
using BuildingManagement.Core.Interfaces;
using BuildingManagement.Infrastructure.Data;
using BuildingManagement.Infrastructure.Services.Sms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BuildingManagement.Api.Controllers;

[ApiController]
[Route("api/notifications/sms")]
[Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
public class SmsNotificationsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ISmsSender _smsSender;
    private readonly SmsRateLimiter _rateLimiter;

    public SmsNotificationsController(AppDbContext db, ISmsSender smsSender, SmsRateLimiter rateLimiter)
    {
        _db = db;
        _smsSender = smsSender;
        _rateLimiter = rateLimiter;
    }

    // ─── Templates ──────────────────────────────────────

    [HttpGet("templates")]
    public async Task<ActionResult<List<SmsTemplateDto>>> GetTemplates([FromQuery] string? lang)
    {
        var q = _db.SmsTemplates.Where(t => t.IsActive);
        if (!string.IsNullOrEmpty(lang))
            q = q.Where(t => t.Language == lang);

        var templates = await q.OrderBy(t => t.Language).ThenBy(t => t.Name)
            .Select(t => new SmsTemplateDto
            {
                Id = t.Id, Name = t.Name, Language = t.Language, Body = t.Body, IsActive = t.IsActive
            }).ToListAsync();
        return Ok(templates);
    }

    // ─── Campaigns ──────────────────────────────────────

    [HttpGet("campaigns")]
    public async Task<ActionResult<List<SmsCampaignDto>>> GetCampaigns([FromQuery] int? buildingId, [FromQuery] string? period)
    {
        var q = _db.SmsCampaigns.Include(c => c.Building).Include(c => c.Template).AsQueryable();
        if (buildingId.HasValue) q = q.Where(c => c.BuildingId == buildingId);
        if (!string.IsNullOrEmpty(period)) q = q.Where(c => c.Period == period);

        var items = await q.OrderByDescending(c => c.CreatedAtUtc).Take(50).ToListAsync();
        return Ok(items.Select(MapCampaignDto).ToList());
    }

    [HttpGet("campaigns/{id}")]
    public async Task<ActionResult<CreateCampaignResult>> GetCampaign(int id)
    {
        var campaign = await _db.SmsCampaigns
            .Include(c => c.Building).Include(c => c.Template).Include(c => c.Recipients)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (campaign == null) return NotFound();

        return Ok(new CreateCampaignResult
        {
            Campaign = MapCampaignDto(campaign),
            Recipients = campaign.Recipients.Select(MapRecipientDto).OrderBy(r => r.FullNameSnapshot).ToList()
        });
    }

    // ─── Create Campaign (Draft + generate recipients) ──

    [HttpPost("hoa-nonpayment/campaigns")]
    public async Task<ActionResult<CreateCampaignResult>> CreateCampaign([FromBody] CreateSmsCampaignRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;

        // Validate building access
        if (!User.IsInRole(AppRoles.Admin))
        {
            var hasAccess = await _db.BuildingManagers.AnyAsync(bm => bm.UserId == userId && bm.BuildingId == request.BuildingId);
            if (!hasAccess) return Forbid();
        }

        var template = await _db.SmsTemplates.FindAsync(request.TemplateId);
        if (template == null) return BadRequest(new { message = "Template not found." });

        var building = await _db.Buildings.FindAsync(request.BuildingId);
        if (building == null) return BadRequest(new { message = "Building not found." });

        // Create campaign
        var campaign = new SmsCampaign
        {
            BuildingId = request.BuildingId,
            Period = request.Period,
            TemplateId = request.TemplateId,
            CreatedByUserId = userId,
            Notes = request.Notes
        };
        _db.SmsCampaigns.Add(campaign);
        await _db.SaveChangesAsync();

        // Generate recipients from collection status
        var recipients = await GenerateRecipients(campaign, request.IncludePartial);
        campaign.TotalSelected = recipients.Count(r => r.IsSelected);

        // Audit
        _db.AuditLogs.Add(new AuditLog
        {
            Action = "CreateSmsCampaign",
            EntityName = "SmsCampaign",
            EntityId = campaign.Id.ToString(),
            PerformedBy = userId,
            Details = $"Created SMS campaign for building {building.Name}, period {request.Period}. Recipients: {recipients.Count}, Selected: {campaign.TotalSelected}"
        });
        await _db.SaveChangesAsync();

        return Ok(new CreateCampaignResult
        {
            Campaign = MapCampaignDto(await _db.SmsCampaigns.Include(c => c.Building).Include(c => c.Template).FirstAsync(c => c.Id == campaign.Id)),
            Recipients = recipients.Select(MapRecipientDto).OrderBy(r => r.FullNameSnapshot).ToList()
        });
    }

    // ─── Update Recipients ──────────────────────────────

    [HttpPut("campaigns/{campaignId}/recipients")]
    public async Task<ActionResult<List<SmsCampaignRecipientDto>>> UpdateRecipients(int campaignId, [FromBody] UpdateRecipientsRequest request)
    {
        var campaign = await _db.SmsCampaigns.Include(c => c.Recipients).FirstOrDefaultAsync(c => c.Id == campaignId);
        if (campaign == null) return NotFound();
        if (campaign.Status != SmsCampaignStatus.Draft)
            return BadRequest(new { message = "Can only update recipients of a draft campaign." });

        // Update selections
        if (request.Updates != null)
        {
            foreach (var update in request.Updates)
            {
                var recipient = campaign.Recipients.FirstOrDefault(r => r.Id == update.RecipientId);
                if (recipient != null)
                    recipient.IsSelected = update.IsSelected;
            }
        }

        // Remove recipients
        if (request.RemoveRecipientIds != null)
        {
            var toRemove = campaign.Recipients.Where(r => request.RemoveRecipientIds.Contains(r.Id)).ToList();
            foreach (var r in toRemove)
                _db.SmsCampaignRecipients.Remove(r);
        }

        // Add new units
        if (request.AddUnitIds != null)
        {
            foreach (var unitId in request.AddUnitIds)
            {
                // Prevent duplicates
                if (campaign.Recipients.Any(r => r.UnitId == unitId)) continue;

                var tenant = await _db.TenantProfiles
                    .Include(tp => tp.Unit)
                    .Where(tp => tp.UnitId == unitId && tp.IsActive && !tp.IsDeleted)
                    .FirstOrDefaultAsync();

                var unit = await _db.Units.FindAsync(unitId);
                if (unit == null) continue;

                var phone = tenant?.Phone ?? unit.TenantUser?.Phone;
                if (string.IsNullOrWhiteSpace(phone))
                    continue; // skip units without phone

                // Get charge info
                var charge = await _db.UnitCharges
                    .Include(uc => uc.Allocations)
                    .Where(uc => uc.UnitId == unitId && uc.Period == campaign.Period)
                    .FirstOrDefaultAsync();

                var amountDue = charge?.AmountDue ?? 0;
                var amountPaid = charge?.Allocations?.Sum(a => a.AllocatedAmount) ?? 0;

                _db.SmsCampaignRecipients.Add(new SmsCampaignRecipient
                {
                    CampaignId = campaignId,
                    UnitId = unitId,
                    TenantProfileId = tenant?.Id,
                    FullNameSnapshot = tenant?.FullName ?? unit.OwnerName ?? "—",
                    PhoneSnapshot = phone,
                    AmountDueSnapshot = amountDue,
                    AmountPaidSnapshot = amountPaid,
                    OutstandingSnapshot = amountDue - amountPaid,
                    ChargeStatusSnapshot = charge == null ? "NotGenerated" : charge.Status.ToString(),
                    IsSelected = false // Manual additions start unchecked
                });
            }
        }

        campaign.TotalSelected = campaign.Recipients.Count(r => r.IsSelected);
        await _db.SaveChangesAsync();

        var updated = await _db.SmsCampaignRecipients.Where(r => r.CampaignId == campaignId).ToListAsync();
        return Ok(updated.Select(MapRecipientDto).OrderBy(r => r.FullNameSnapshot).ToList());
    }

    // ─── Preview Message ────────────────────────────────

    [HttpGet("campaigns/{campaignId}/recipients/{recipientId}/preview")]
    public async Task<ActionResult<object>> PreviewMessage(int campaignId, int recipientId)
    {
        var campaign = await _db.SmsCampaigns.Include(c => c.Template).Include(c => c.Building).FirstOrDefaultAsync(c => c.Id == campaignId);
        if (campaign == null) return NotFound();

        var recipient = await _db.SmsCampaignRecipients.FirstOrDefaultAsync(r => r.Id == recipientId && r.CampaignId == campaignId);
        if (recipient == null) return NotFound();

        var message = RenderTemplate(campaign.Template.Body, campaign, recipient);
        return Ok(new { message });
    }

    // ─── Send Campaign ──────────────────────────────────

    [HttpPost("campaigns/{campaignId}/send")]
    public async Task<ActionResult<SendCampaignResult>> SendCampaign(int campaignId, [FromBody] SendCampaignRequest request)
    {
        if (!request.Confirm)
            return BadRequest(new { message = "Must confirm before sending." });

        var campaign = await _db.SmsCampaigns
            .Include(c => c.Template).Include(c => c.Building).Include(c => c.Recipients)
            .FirstOrDefaultAsync(c => c.Id == campaignId);
        if (campaign == null) return NotFound();
        if (campaign.Status != SmsCampaignStatus.Draft)
            return BadRequest(new { message = "Campaign has already been sent or cancelled." });

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
        var selectedRecipients = campaign.Recipients.Where(r => r.IsSelected && r.SendStatus == SmsSendStatus.Pending).ToList();

        int sentCount = 0, failedCount = 0, skippedCount = 0;

        foreach (var recipient in selectedRecipients)
        {
            // Normalize & validate phone
            var e164 = PhoneNormalizer.NormalizeIsraeli(recipient.PhoneSnapshot);
            if (string.IsNullOrEmpty(e164) || !PhoneNormalizer.IsValidIsraeliMobile(e164))
            {
                recipient.SendStatus = SmsSendStatus.Skipped;
                recipient.ErrorMessage = "Invalid or missing phone number";
                skippedCount++;
                continue;
            }

            var message = RenderTemplate(campaign.Template.Body, campaign, recipient);

            try
            {
                await _rateLimiter.WaitForSlotAsync();
                var result = await _smsSender.SendAsync(e164, message);

                if (result.Success)
                {
                    recipient.SendStatus = SmsSendStatus.Sent;
                    recipient.ProviderMessageId = result.MessageId;
                    recipient.SentAtUtc = DateTime.UtcNow;
                    sentCount++;
                }
                else
                {
                    recipient.SendStatus = SmsSendStatus.Failed;
                    recipient.ErrorMessage = result.Error ?? "Send failed";
                    failedCount++;
                }
            }
            catch (Exception ex)
            {
                recipient.SendStatus = SmsSendStatus.Failed;
                recipient.ErrorMessage = ex.Message;
                failedCount++;
            }
        }

        campaign.Status = SmsCampaignStatus.Sent;
        campaign.TotalSelected = selectedRecipients.Count;
        campaign.SentCount = sentCount;
        campaign.FailedCount = failedCount;
        campaign.SkippedCount = skippedCount;
        campaign.SentAtUtc = DateTime.UtcNow;

        // Audit
        _db.AuditLogs.Add(new AuditLog
        {
            Action = "SendSmsCampaign",
            EntityName = "SmsCampaign",
            EntityId = campaign.Id.ToString(),
            PerformedBy = userId,
            Details = $"Sent SMS campaign #{campaign.Id}. Selected: {selectedRecipients.Count}, Sent: {sentCount}, Failed: {failedCount}, Skipped: {skippedCount}"
        });

        await _db.SaveChangesAsync();

        return Ok(new SendCampaignResult
        {
            TotalSelected = selectedRecipients.Count,
            SentCount = sentCount,
            FailedCount = failedCount,
            SkippedCount = skippedCount
        });
    }

    // ─── Helpers ─────────────────────────────────────────

    private async Task<List<SmsCampaignRecipient>> GenerateRecipients(SmsCampaign campaign, bool includePartial)
    {
        var units = await _db.Units
            .Where(u => u.BuildingId == campaign.BuildingId && !u.IsDeleted)
            .ToListAsync();

        var recipients = new List<SmsCampaignRecipient>();

        foreach (var unit in units)
        {
            // Get active tenant
            var tenant = await _db.TenantProfiles
                .Where(tp => tp.UnitId == unit.Id && tp.IsActive && !tp.IsDeleted)
                .FirstOrDefaultAsync();

            var phone = tenant?.Phone;
            if (string.IsNullOrWhiteSpace(phone)) continue; // skip units without phone

            // Get charge
            var charge = await _db.UnitCharges
                .Include(uc => uc.Allocations)
                .Where(uc => uc.UnitId == unit.Id && uc.Period == campaign.Period)
                .FirstOrDefaultAsync();

            var amountDue = charge?.AmountDue ?? 0;
            var amountPaid = charge?.Allocations?.Sum(a => a.AllocatedAmount) ?? 0;
            var outstanding = amountDue - amountPaid;

            // Determine status
            string status;
            if (charge == null)
                status = "NotGenerated";
            else if (amountPaid >= amountDue && amountDue > 0)
                status = "Paid";
            else if (amountPaid > 0)
                status = "Partial";
            else if (charge.DueDate < DateTime.UtcNow)
                status = "Overdue";
            else
                status = "Unpaid";

            // Selection logic
            bool selected = status switch
            {
                "Unpaid" => true,
                "Overdue" => true,
                "Partial" => includePartial,
                _ => false
            };

            var recipient = new SmsCampaignRecipient
            {
                CampaignId = campaign.Id,
                UnitId = unit.Id,
                TenantProfileId = tenant?.Id,
                FullNameSnapshot = tenant?.FullName ?? unit.OwnerName ?? "—",
                PhoneSnapshot = phone,
                AmountDueSnapshot = amountDue,
                AmountPaidSnapshot = amountPaid,
                OutstandingSnapshot = outstanding,
                ChargeStatusSnapshot = status,
                IsSelected = selected
            };

            _db.SmsCampaignRecipients.Add(recipient);
            recipients.Add(recipient);
        }

        await _db.SaveChangesAsync();
        return recipients;
    }

    private static string RenderTemplate(string templateBody, SmsCampaign campaign, SmsCampaignRecipient recipient)
    {
        var payLink = "https://app.homehero.co.il/my-charges"; // MVP placeholder

        return templateBody
            .Replace("{{FullName}}", recipient.FullNameSnapshot)
            .Replace("{{BuildingName}}", campaign.Building?.Name ?? "")
            .Replace("{{Period}}", campaign.Period)
            .Replace("{{AmountDue}}", recipient.AmountDueSnapshot.ToString("F2"))
            .Replace("{{Outstanding}}", recipient.OutstandingSnapshot.ToString("F2"))
            .Replace("{{PayLink}}", payLink);
    }

    private static SmsCampaignDto MapCampaignDto(SmsCampaign c) => new()
    {
        Id = c.Id,
        BuildingId = c.BuildingId,
        BuildingName = c.Building?.Name,
        Period = c.Period,
        TemplateId = c.TemplateId,
        TemplateName = c.Template?.Name,
        CreatedByUserId = c.CreatedByUserId,
        CreatedAtUtc = c.CreatedAtUtc,
        Status = c.Status,
        Notes = c.Notes,
        TotalSelected = c.TotalSelected,
        SentCount = c.SentCount,
        FailedCount = c.FailedCount,
        SkippedCount = c.SkippedCount,
        SentAtUtc = c.SentAtUtc
    };

    private static SmsCampaignRecipientDto MapRecipientDto(SmsCampaignRecipient r) => new()
    {
        Id = r.Id,
        UnitId = r.UnitId,
        TenantProfileId = r.TenantProfileId,
        FullNameSnapshot = r.FullNameSnapshot,
        PhoneSnapshot = r.PhoneSnapshot,
        AmountDueSnapshot = r.AmountDueSnapshot,
        AmountPaidSnapshot = r.AmountPaidSnapshot,
        OutstandingSnapshot = r.OutstandingSnapshot,
        ChargeStatusSnapshot = r.ChargeStatusSnapshot,
        IsSelected = r.IsSelected,
        SendStatus = r.SendStatus,
        ProviderMessageId = r.ProviderMessageId,
        ErrorMessage = r.ErrorMessage,
        SentAtUtc = r.SentAtUtc
    };
}
