using BuildingManagement.Api.Hubs;
using BuildingManagement.Core.DTOs;
using BuildingManagement.Core.Entities;
using BuildingManagement.Core.Enums;
using BuildingManagement.Core.Interfaces;
using BuildingManagement.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
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
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<TicketChatHub> _hubContext;
    private readonly ILogger<TicketMessagesController> _logger;

    public TicketMessagesController(AppDbContext db, ITicketAiAgent aiAgent, IServiceScopeFactory scopeFactory, IHubContext<TicketChatHub> hubContext, ILogger<TicketMessagesController> logger)
    {
        _db = db;
        _aiAgent = aiAgent;
        _scopeFactory = scopeFactory;
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>Get message thread for a ticket.</summary>
    [HttpGet("{id}/messages")]
    public async Task<ActionResult<List<TicketMessageDto>>> GetMessages(int id)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
        var sr = await _db.ServiceRequests.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
        if (sr == null) return NotFound();

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

    /// <summary>Mark messages as read up to the latest message in the thread.</summary>
    [HttpPost("{id}/mark-read")]
    public async Task<IActionResult> MarkRead(int id)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
        var lastMsgId = await _db.TicketMessages
            .Where(m => m.ServiceRequestId == id)
            .OrderByDescending(m => m.Id)
            .Select(m => m.Id)
            .FirstOrDefaultAsync();

        if (lastMsgId == 0) return Ok();

        var receipt = await _db.TicketReadReceipts
            .FirstOrDefaultAsync(r => r.ServiceRequestId == id && r.UserId == userId);

        if (receipt == null)
        {
            _db.TicketReadReceipts.Add(new TicketReadReceipt
            {
                ServiceRequestId = id,
                UserId = userId,
                LastReadMessageId = lastMsgId
            });
        }
        else if (lastMsgId > receipt.LastReadMessageId)
        {
            receipt.LastReadMessageId = lastMsgId;
            receipt.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        return Ok();
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

        // Broadcast via SignalR
        await BroadcastMessageAsync(id, MapDto(message));

        // If tenant posted, trigger AI agent response in a new scope (fire-and-forget)
        if (senderType == TicketMessageSender.Tenant)
        {
            var ticketId = id;
            var factory = _scopeFactory;
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = factory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var agent = scope.ServiceProvider.GetRequiredService<ITicketAiAgent>();
                    var hub = scope.ServiceProvider.GetRequiredService<IHubContext<TicketChatHub>>();
                    await GenerateAgentReplyInScopeAsync(db, agent, hub, ticketId);
                }
                catch (Exception ex) { Console.WriteLine($"Agent reply failed for ticket #{ticketId}: {ex}"); }
            });
        }

        return Ok(MapDto(message));
    }

    /// <summary>
    /// Called after a new ticket is created to trigger the AI agent's initial analysis.
    /// </summary>
    public async Task TriggerAgentOnNewTicketAsync(int serviceRequestId)
    {
        try
        {
            var sr = await _db.ServiceRequests
                .Include(s => s.Building)
                .Include(s => s.Unit)
                .FirstOrDefaultAsync(s => s.Id == serviceRequestId);
            if (sr == null) { _logger.LogWarning("TriggerAgentOnNewTicket: SR #{Id} not found", serviceRequestId); return; }

            var openTickets = await _db.ServiceRequests
                .Where(s => s.BuildingId == sr.BuildingId && s.Id != sr.Id &&
                            s.Status != ServiceRequestStatus.Resolved &&
                            s.Status != ServiceRequestStatus.Closed &&
                            s.Status != ServiceRequestStatus.Rejected)
                .Select(s => new TicketSummary(s.Id, s.Area.ToString(), s.Category.ToString(), s.Description, s.IncidentGroupId))
                .ToListAsync();

            _logger.LogInformation("TriggerAgentOnNewTicket: SR #{Id}, found {Count} open tickets in building", serviceRequestId, openTickets.Count);

            var ctx = BuildContext(sr);
            var result = await _aiAgent.AnalyzeNewTicketAsync(ctx, openTickets);

            // Handle incident clustering
            if (result.MatchedIncidentGroupId.HasValue)
            {
                sr.IncidentGroupId = result.MatchedIncidentGroupId.Value;
                await _db.SaveChangesAsync();
                _logger.LogInformation("SR #{Id} linked to existing incident group #{GroupId}", serviceRequestId, result.MatchedIncidentGroupId.Value);
            }
            else if (result.IncidentTitle != null)
            {
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
                    _logger.LogInformation("Created incident group #{GroupId} with {Count} tickets", group.Id, matchedTickets.Count + 1);
                }
            }

            // Post agent message
            if (!string.IsNullOrEmpty(result.Message))
            {
                var agentMsg = new TicketMessage
                {
                    ServiceRequestId = serviceRequestId,
                    SenderType = TicketMessageSender.Agent,
                    SenderName = "AI Assistant",
                    Text = result.Message
                };
                _db.TicketMessages.Add(agentMsg);
                await _db.SaveChangesAsync();
                await BroadcastMessageAsync(serviceRequestId, MapDto(agentMsg));
                _logger.LogInformation("Agent message posted for SR #{Id}", serviceRequestId);
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
            var msgText = await _aiAgent.GenerateResolutionFollowUpAsync(ctx);

            var agentMsg = new TicketMessage
            {
                ServiceRequestId = serviceRequestId,
                SenderType = TicketMessageSender.Agent,
                SenderName = "AI Assistant",
                Text = msgText
            };
            _db.TicketMessages.Add(agentMsg);
            await _db.SaveChangesAsync();
            await BroadcastMessageAsync(serviceRequestId, MapDto(agentMsg));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Agent resolution follow-up failed for ticket #{Id}", serviceRequestId);
        }
    }

    private static async Task GenerateAgentReplyInScopeAsync(AppDbContext db, ITicketAiAgent agent, IHubContext<TicketChatHub> hub, int serviceRequestId)
    {
        var sr = await db.ServiceRequests
            .Include(s => s.Building)
            .Include(s => s.Unit)
            .FirstOrDefaultAsync(s => s.Id == serviceRequestId);
        if (sr == null) return;

        var messages = await db.TicketMessages
            .Where(m => m.ServiceRequestId == serviceRequestId)
            .OrderBy(m => m.CreatedAtUtc)
            .Select(m => new MessageEntry(m.SenderType.ToString(), m.Text))
            .ToListAsync();

        var ctx = BuildContext(sr);
        var result = await agent.ProcessTenantReplyAsync(ctx, messages);

        var fieldsChanged = ApplyFieldUpdates(sr, result.FieldUpdates);

        if (!string.IsNullOrEmpty(result.Message))
        {
            var agentMsg = new TicketMessage
            {
                ServiceRequestId = serviceRequestId,
                SenderType = TicketMessageSender.Agent,
                SenderName = "AI Assistant",
                Text = result.Message
            };
            db.TicketMessages.Add(agentMsg);
            await db.SaveChangesAsync();
            await hub.Clients.Group($"ticket-{serviceRequestId}").SendAsync("NewMessage", MapDto(agentMsg));
        }
        else
        {
            await db.SaveChangesAsync();
        }

        if (fieldsChanged)
        {
            await hub.Clients.Group($"ticket-{serviceRequestId}").SendAsync("TicketUpdated", new
            {
                ticketId = serviceRequestId,
                area = sr.Area.ToString(),
                category = sr.Category.ToString(),
                priority = sr.Priority.ToString(),
                isEmergency = sr.IsEmergency,
                description = sr.Description
            });
        }
    }

    private static bool ApplyFieldUpdates(ServiceRequest sr, TicketFieldUpdates? updates)
    {
        if (updates == null) return false;
        var changed = false;

        if (updates.Area != null && Enum.TryParse<Area>(updates.Area, ignoreCase: true, out var area) && sr.Area != area)
        {
            sr.Area = area;
            changed = true;
        }
        if (updates.Category != null && Enum.TryParse<ServiceRequestCategory>(updates.Category, ignoreCase: true, out var cat) && sr.Category != cat)
        {
            sr.Category = cat;
            changed = true;
        }
        if (updates.Priority != null && Enum.TryParse<Priority>(updates.Priority, ignoreCase: true, out var pri) && sr.Priority != pri)
        {
            sr.Priority = pri;
            changed = true;
        }
        if (updates.IsEmergency.HasValue && sr.IsEmergency != updates.IsEmergency.Value)
        {
            sr.IsEmergency = updates.IsEmergency.Value;
            changed = true;
        }
        if (!string.IsNullOrWhiteSpace(updates.Description) && sr.Description != updates.Description)
        {
            sr.Description = updates.Description;
            changed = true;
        }

        if (changed) sr.UpdatedAtUtc = DateTime.UtcNow;
        return changed;
    }

    private async Task BroadcastMessageAsync(int ticketId, TicketMessageDto dto)
    {
        try { await _hubContext.Clients.Group($"ticket-{ticketId}").SendAsync("NewMessage", dto); }
        catch (Exception ex) { _logger.LogWarning(ex, "SignalR broadcast failed for ticket #{Id}", ticketId); }
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
