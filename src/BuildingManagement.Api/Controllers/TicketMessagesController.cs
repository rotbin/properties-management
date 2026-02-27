using BuildingManagement.Core.DTOs;
using BuildingManagement.Core.Entities;
using BuildingManagement.Core.Enums;
using BuildingManagement.Core.Interfaces;
using BuildingManagement.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BuildingManagement.Api.Controllers;

[ApiController]
[Route("api/tickets")]
[Authorize]
public class TicketMessagesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITicketAiAgent _aiAgent;
    private readonly ILogger<TicketMessagesController> _logger;

    public TicketMessagesController(AppDbContext db, ITicketAiAgent aiAgent, ILogger<TicketMessagesController> logger)
    {
        _db = db;
        _aiAgent = aiAgent;
        _logger = logger;
    }

    /// <summary>Get message thread for a ticket.</summary>
    [HttpGet("{id}/messages")]
    public async Task<ActionResult<List<TicketMessageDto>>> GetMessages(int id)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
        var sr = await _db.ServiceRequests.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
        if (sr == null) return NotFound();

        // Tenants can only see their own tickets
        if (User.IsInRole(AppRoles.Tenant) && !User.IsInRole(AppRoles.Admin) && !User.IsInRole(AppRoles.Manager))
        {
            if (sr.SubmittedByUserId != userId) return Forbid();
        }

        var messages = await _db.TicketMessages
            .Where(m => m.ServiceRequestId == id)
            .OrderBy(m => m.CreatedAtUtc)
            .Select(m => new TicketMessageDto
            {
                Id = m.Id,
                ServiceRequestId = m.ServiceRequestId,
                SenderType = m.SenderType.ToString(),
                SenderUserId = m.SenderUserId,
                SenderName = m.SenderName,
                Text = m.Text,
                CreatedAtUtc = m.CreatedAtUtc
            })
            .ToListAsync();

        return Ok(messages);
    }

    /// <summary>Post a message to a ticket thread. Triggers AI agent response if sender is tenant.</summary>
    [HttpPost("{id}/messages")]
    public async Task<ActionResult<TicketMessageDto>> PostMessage(int id, [FromBody] PostTicketMessageRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return Unauthorized();

        var sr = await _db.ServiceRequests
            .Include(s => s.Building)
            .Include(s => s.Unit)
            .FirstOrDefaultAsync(s => s.Id == id);
        if (sr == null) return NotFound();

        // Determine sender type
        TicketMessageSender senderType;
        if (User.IsInRole(AppRoles.Tenant) && sr.SubmittedByUserId == userId)
            senderType = TicketMessageSender.Tenant;
        else if (User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.Manager))
            senderType = TicketMessageSender.Manager;
        else
            return Forbid();

        var message = new TicketMessage
        {
            ServiceRequestId = id,
            SenderType = senderType,
            SenderUserId = userId,
            SenderName = user.FullName,
            Text = request.Text
        };

        _db.TicketMessages.Add(message);
        await _db.SaveChangesAsync();

        // If tenant posted, trigger AI agent response asynchronously
        if (senderType == TicketMessageSender.Tenant)
        {
            _ = Task.Run(async () =>
            {
                try { await GenerateAgentReplyAsync(sr, id); }
                catch (Exception ex) { _logger.LogError(ex, "Agent reply failed for ticket #{Id}", id); }
            });
        }

        return Ok(MapDto(message));
    }

    /// <summary>
    /// Called after a new ticket is created to trigger the AI agent's initial analysis.
    /// This is a fire-and-forget internal method called from ServiceRequestsController.
    /// </summary>
    public async Task TriggerAgentOnNewTicketAsync(int serviceRequestId)
    {
        try
        {
            var sr = await _db.ServiceRequests
                .Include(s => s.Building)
                .Include(s => s.Unit)
                .FirstOrDefaultAsync(s => s.Id == serviceRequestId);
            if (sr == null) return;

            var openTickets = await _db.ServiceRequests
                .Where(s => s.BuildingId == sr.BuildingId && s.Id != sr.Id &&
                            s.Status != ServiceRequestStatus.Resolved &&
                            s.Status != ServiceRequestStatus.Closed &&
                            s.Status != ServiceRequestStatus.Rejected)
                .Select(s => new TicketSummary(s.Id, s.Area.ToString(), s.Category.ToString(), s.Description, s.IncidentGroupId))
                .ToListAsync();

            var ctx = BuildContext(sr);
            var result = await _aiAgent.AnalyzeNewTicketAsync(ctx, openTickets);

            // Handle incident clustering
            if (result.MatchedIncidentGroupId.HasValue)
            {
                sr.IncidentGroupId = result.MatchedIncidentGroupId.Value;
                await _db.SaveChangesAsync();
            }
            else if (result.IncidentTitle != null)
            {
                // Find the matched ticket and create a new incident group
                var matchedTickets = await _db.ServiceRequests
                    .Where(s => s.BuildingId == sr.BuildingId && s.Id != sr.Id &&
                                s.IncidentGroupId == null &&
                                s.Area == sr.Area && s.Category == sr.Category &&
                                s.Status != ServiceRequestStatus.Resolved &&
                                s.Status != ServiceRequestStatus.Closed)
                    .ToListAsync();

                if (matchedTickets.Count > 0)
                {
                    var group = new IncidentGroup
                    {
                        BuildingId = sr.BuildingId,
                        Title = result.IncidentTitle
                    };
                    _db.IncidentGroups.Add(group);
                    await _db.SaveChangesAsync();

                    sr.IncidentGroupId = group.Id;
                    foreach (var mt in matchedTickets) mt.IncidentGroupId = group.Id;
                    await _db.SaveChangesAsync();
                }
            }

            // Post agent message
            if (!string.IsNullOrEmpty(result.Message))
            {
                _db.TicketMessages.Add(new TicketMessage
                {
                    ServiceRequestId = serviceRequestId,
                    SenderType = TicketMessageSender.Agent,
                    SenderName = "AI Assistant",
                    Text = result.Message
                });
                await _db.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Agent analysis failed for ticket #{Id}", serviceRequestId);
        }
    }

    /// <summary>
    /// Called when a ticket status changes to Resolved to trigger satisfaction follow-up.
    /// </summary>
    public async Task TriggerAgentOnResolutionAsync(int serviceRequestId)
    {
        try
        {
            var sr = await _db.ServiceRequests
                .Include(s => s.Building)
                .Include(s => s.Unit)
                .FirstOrDefaultAsync(s => s.Id == serviceRequestId);
            if (sr == null) return;

            var ctx = BuildContext(sr);
            var message = await _aiAgent.GenerateResolutionFollowUpAsync(ctx);

            _db.TicketMessages.Add(new TicketMessage
            {
                ServiceRequestId = serviceRequestId,
                SenderType = TicketMessageSender.Agent,
                SenderName = "AI Assistant",
                Text = message
            });
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Agent resolution follow-up failed for ticket #{Id}", serviceRequestId);
        }
    }

    private async Task GenerateAgentReplyAsync(ServiceRequest sr, int serviceRequestId)
    {
        var messages = await _db.TicketMessages
            .Where(m => m.ServiceRequestId == serviceRequestId)
            .OrderBy(m => m.CreatedAtUtc)
            .Select(m => new MessageEntry(m.SenderType.ToString(), m.Text))
            .ToListAsync();

        var ctx = BuildContext(sr);
        var reply = await _aiAgent.ProcessTenantReplyAsync(ctx, messages);

        if (!string.IsNullOrEmpty(reply))
        {
            _db.TicketMessages.Add(new TicketMessage
            {
                ServiceRequestId = serviceRequestId,
                SenderType = TicketMessageSender.Agent,
                SenderName = "AI Assistant",
                Text = reply
            });
            await _db.SaveChangesAsync();
        }
    }

    private static TicketContext BuildContext(ServiceRequest sr) => new(
        Id: sr.Id,
        BuildingName: sr.Building?.Name ?? "",
        UnitNumber: sr.Unit?.UnitNumber,
        Area: sr.Area.ToString(),
        Category: sr.Category.ToString(),
        Priority: sr.Priority.ToString(),
        IsEmergency: sr.IsEmergency,
        Description: sr.Description,
        TenantName: sr.SubmittedByName
    );

    private static TicketMessageDto MapDto(TicketMessage m) => new()
    {
        Id = m.Id,
        ServiceRequestId = m.ServiceRequestId,
        SenderType = m.SenderType.ToString(),
        SenderUserId = m.SenderUserId,
        SenderName = m.SenderName,
        Text = m.Text,
        CreatedAtUtc = m.CreatedAtUtc
    };
}
