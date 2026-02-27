using BuildingManagement.Core.Entities;
using BuildingManagement.Core.Enums;
using BuildingManagement.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BuildingManagement.Api.Hubs;

[Authorize]
public class TicketChatHub : Hub
{
    private readonly AppDbContext _db;

    public TicketChatHub(AppDbContext db) => _db = db;

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId != null)
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");
        await base.OnConnectedAsync();
    }

    /// <summary>Client joins a ticket's message group to receive real-time updates.</summary>
    public async Task JoinTicket(int ticketId)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return;

        var sr = await _db.ServiceRequests.AsNoTracking().FirstOrDefaultAsync(s => s.Id == ticketId);
        if (sr == null) return;

        var isTenant = Context.User!.IsInRole("Tenant") && !Context.User.IsInRole("Admin") && !Context.User.IsInRole("Manager");
        if (isTenant && sr.SubmittedByUserId != userId) return;

        await Groups.AddToGroupAsync(Context.ConnectionId, $"ticket-{ticketId}");
    }

    /// <summary>Client leaves a ticket's message group.</summary>
    public async Task LeaveTicket(int ticketId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"ticket-{ticketId}");
    }

    /// <summary>Client marks messages as read up to a given message ID.</summary>
    public async Task MarkRead(int ticketId, int lastMessageId)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return;

        var receipt = await _db.TicketReadReceipts
            .FirstOrDefaultAsync(r => r.ServiceRequestId == ticketId && r.UserId == userId);

        if (receipt == null)
        {
            _db.TicketReadReceipts.Add(new TicketReadReceipt
            {
                ServiceRequestId = ticketId,
                UserId = userId,
                LastReadMessageId = lastMessageId
            });
        }
        else if (lastMessageId > receipt.LastReadMessageId)
        {
            receipt.LastReadMessageId = lastMessageId;
            receipt.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
    }
}
