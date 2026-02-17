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
    private readonly IEmailSender _emailSender;
    private readonly SmsRateLimiter _rateLimiter;

    public SmsNotificationsController(AppDbContext db, ISmsSender smsSender, IEmailSender emailSender, SmsRateLimiter rateLimiter)
    {
        _db = db;
        _smsSender = smsSender;
        _emailSender = emailSender;
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
                Id = t.Id, Name = t.Name, Language = t.Language, Body = t.Body,
                EmailSubject = t.EmailSubject, IsActive = t.IsActive
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

        if (!User.IsInRole(AppRoles.Admin))
        {
            var hasAccess = await _db.BuildingManagers.AnyAsync(bm => bm.UserId == userId && bm.BuildingId == request.BuildingId);
            if (!hasAccess) return Forbid();
        }

        var template = await _db.SmsTemplates.FindAsync(request.TemplateId);
        if (template == null) return BadRequest(new { message = "Template not found." });

        var building = await _db.Buildings.FindAsync(request.BuildingId);
        if (building == null) return BadRequest(new { message = "Building not found." });

        var campaign = new SmsCampaign
        {
            BuildingId = request.BuildingId,
            Period = request.Period,
            TemplateId = request.TemplateId,
            CreatedByUserId = userId,
            Channel = request.Channel,
            Notes = request.Notes
        };
        _db.SmsCampaigns.Add(campaign);
        await _db.SaveChangesAsync();

        var recipients = await GenerateRecipients(campaign, request.IncludePartial);
        campaign.TotalSelected = recipients.Count(r => r.IsSelected);

        _db.AuditLogs.Add(new AuditLog
        {
            Action = "CreateReminderCampaign",
            EntityName = "SmsCampaign",
            EntityId = campaign.Id.ToString(),
            PerformedBy = userId,
            Details = $"Created {campaign.Channel} reminder campaign for building {building.Name}, period {request.Period}. Recipients: {recipients.Count}, Selected: {campaign.TotalSelected}"
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

        if (request.Updates != null)
        {
            foreach (var update in request.Updates)
            {
                var recipient = campaign.Recipients.FirstOrDefault(r => r.Id == update.RecipientId);
                if (recipient != null)
                    recipient.IsSelected = update.IsSelected;
            }
        }

        if (request.RemoveRecipientIds != null)
        {
            var toRemove = campaign.Recipients.Where(r => request.RemoveRecipientIds.Contains(r.Id)).ToList();
            foreach (var r in toRemove)
                _db.SmsCampaignRecipients.Remove(r);
        }

        if (request.AddUnitIds != null)
        {
            foreach (var unitId in request.AddUnitIds)
            {
                if (campaign.Recipients.Any(r => r.UnitId == unitId)) continue;

                var tenant = await _db.TenantProfiles
                    .Include(tp => tp.Unit)
                    .Where(tp => tp.UnitId == unitId && tp.IsActive && !tp.IsDeleted)
                    .FirstOrDefaultAsync();

                var unit = await _db.Units.FindAsync(unitId);
                if (unit == null) continue;

                var phone = tenant?.Phone ?? unit.TenantUser?.Phone;
                var email = tenant?.Email ?? unit.TenantUser?.Email;

                // For SMS channel, skip units without phone; for Email, skip without email
                var hasContact = campaign.Channel switch
                {
                    ReminderChannel.Sms => !string.IsNullOrWhiteSpace(phone),
                    ReminderChannel.Email => !string.IsNullOrWhiteSpace(email),
                    ReminderChannel.Both => !string.IsNullOrWhiteSpace(phone) || !string.IsNullOrWhiteSpace(email),
                    _ => !string.IsNullOrWhiteSpace(phone)
                };
                if (!hasContact) continue;

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
                    EmailSnapshot = email,
                    AmountDueSnapshot = amountDue,
                    AmountPaidSnapshot = amountPaid,
                    OutstandingSnapshot = amountDue - amountPaid,
                    ChargeStatusSnapshot = charge == null ? "NotGenerated" : charge.Status.ToString(),
                    IsSelected = false
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
        var subject = campaign.Template.EmailSubject != null
            ? RenderTemplate(campaign.Template.EmailSubject, campaign, recipient)
            : null;
        return Ok(new { message, subject });
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
            var message = RenderTemplate(campaign.Template.Body, campaign, recipient);
            var subject = campaign.Template.EmailSubject != null
                ? RenderTemplate(campaign.Template.EmailSubject, campaign, recipient)
                : "Payment Reminder";

            bool smsSent = false, emailSent = false;
            string? lastError = null;

            // Send SMS if channel is Sms or Both
            if (campaign.Channel is ReminderChannel.Sms or ReminderChannel.Both)
            {
                var e164 = PhoneNormalizer.NormalizeIsraeli(recipient.PhoneSnapshot);
                if (!string.IsNullOrEmpty(e164) && PhoneNormalizer.IsValidIsraeliMobile(e164))
                {
                    try
                    {
                        await _rateLimiter.WaitForSlotAsync();
                        var smsResult = await _smsSender.SendAsync(e164, message);
                        if (smsResult.Success)
                        {
                            smsSent = true;
                            recipient.ProviderMessageId = smsResult.MessageId;
                        }
                        else
                        {
                            lastError = smsResult.Error ?? "SMS send failed";
                        }
                    }
                    catch (Exception ex)
                    {
                        lastError = $"SMS: {ex.Message}";
                    }
                }
                else if (campaign.Channel == ReminderChannel.Sms)
                {
                    recipient.SendStatus = SmsSendStatus.Skipped;
                    recipient.ErrorMessage = "Invalid or missing phone number";
                    skippedCount++;
                    continue;
                }
            }

            // Send Email if channel is Email or Both
            if (campaign.Channel is ReminderChannel.Email or ReminderChannel.Both)
            {
                var email = recipient.EmailSnapshot;
                if (!string.IsNullOrWhiteSpace(email))
                {
                    try
                    {
                        var htmlBody = $"<div dir=\"rtl\" style=\"font-family:Arial,sans-serif;font-size:14px;line-height:1.6\">{message.Replace("\n", "<br/>")}</div>";
                        var emailResult = await _emailSender.SendAsync(email, subject, htmlBody);
                        if (emailResult.Success)
                        {
                            emailSent = true;
                            recipient.ProviderMessageId ??= emailResult.MessageId;
                        }
                        else
                        {
                            lastError = emailResult.Error ?? "Email send failed";
                        }
                    }
                    catch (Exception ex)
                    {
                        lastError = $"Email: {ex.Message}";
                    }
                }
                else if (campaign.Channel == ReminderChannel.Email)
                {
                    recipient.SendStatus = SmsSendStatus.Skipped;
                    recipient.ErrorMessage = "Missing email address";
                    skippedCount++;
                    continue;
                }
            }

            // Determine final status
            if (smsSent || emailSent)
            {
                recipient.SendStatus = SmsSendStatus.Sent;
                recipient.SentAtUtc = DateTime.UtcNow;
                sentCount++;
            }
            else
            {
                recipient.SendStatus = SmsSendStatus.Failed;
                recipient.ErrorMessage = lastError ?? "No contact info available";
                failedCount++;
            }
        }

        campaign.Status = SmsCampaignStatus.Sent;
        campaign.TotalSelected = selectedRecipients.Count;
        campaign.SentCount = sentCount;
        campaign.FailedCount = failedCount;
        campaign.SkippedCount = skippedCount;
        campaign.SentAtUtc = DateTime.UtcNow;

        _db.AuditLogs.Add(new AuditLog
        {
            Action = "SendReminderCampaign",
            EntityName = "SmsCampaign",
            EntityId = campaign.Id.ToString(),
            PerformedBy = userId,
            Details = $"Sent {campaign.Channel} reminder campaign #{campaign.Id}. Selected: {selectedRecipients.Count}, Sent: {sentCount}, Failed: {failedCount}, Skipped: {skippedCount}"
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
            var tenant = await _db.TenantProfiles
                .Where(tp => tp.UnitId == unit.Id && tp.IsActive && !tp.IsDeleted)
                .FirstOrDefaultAsync();

            var phone = tenant?.Phone;
            var email = tenant?.Email;

            // Check if we have the required contact info for the chosen channel
            var hasContact = campaign.Channel switch
            {
                ReminderChannel.Sms => !string.IsNullOrWhiteSpace(phone),
                ReminderChannel.Email => !string.IsNullOrWhiteSpace(email),
                ReminderChannel.Both => !string.IsNullOrWhiteSpace(phone) || !string.IsNullOrWhiteSpace(email),
                _ => !string.IsNullOrWhiteSpace(phone)
            };
            if (!hasContact) continue;

            var charge = await _db.UnitCharges
                .Include(uc => uc.Allocations)
                .Where(uc => uc.UnitId == unit.Id && uc.Period == campaign.Period)
                .FirstOrDefaultAsync();

            var amountDue = charge?.AmountDue ?? 0;
            var amountPaid = charge?.Allocations?.Sum(a => a.AllocatedAmount) ?? 0;
            var outstanding = amountDue - amountPaid;

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
                EmailSnapshot = email,
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
        var payLink = "https://app.homehero.co.il/my-charges";

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
        Channel = c.Channel,
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
        EmailSnapshot = r.EmailSnapshot,
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
