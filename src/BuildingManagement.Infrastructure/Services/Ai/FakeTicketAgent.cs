using BuildingManagement.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace BuildingManagement.Infrastructure.Services.Ai;

/// <summary>
/// Fake AI agent for development/demo. Returns canned responses with zero external cost.
/// </summary>
public class FakeTicketAgent : ITicketAiAgent
{
    private readonly ILogger<FakeTicketAgent> _logger;

    public FakeTicketAgent(ILogger<FakeTicketAgent> logger) => _logger = logger;

    public Task<AgentAnalysisResult> AnalyzeNewTicketAsync(
        TicketContext ticket, List<TicketSummary> openTicketsInBuilding, CancellationToken ct = default)
    {
        _logger.LogInformation("[FakeAI] Analyzing ticket #{Id}: {Desc}", ticket.Id, ticket.Description);

        var he = IsHebrew(ticket.Description);
        var desc = ticket.Description.ToLowerInvariant();
        var missingParts = new List<string>();

        if (desc.Length < 30)
            missingParts.Add(he ? "תיאור מפורט יותר של הבעיה" : "a more detailed description of the issue");
        if (!ContainsLocationHint(desc) && ticket.Area == "Other")
            missingParts.Add(he ? "המיקום המדויק (קומה, אזור או דירה)" : "the exact location (floor, area, or apartment)");
        if (!ContainsTimeHint(desc))
            missingParts.Add(he ? "מתי הבעיה התחילה או זוהתה לראשונה" : "when this issue started or was first noticed");

        string? message;
        if (missingParts.Count > 0)
        {
            message = he
                ? $"שלום {ticket.TenantName}, תודה על הדיווח. כדי שנוכל לטפל בבעיה במהירות, נשמח אם תוכל/י לספק:\n" +
                  string.Join("\n", missingParts.Select((p, i) => $"{i + 1}. {p}")) +
                  "\n\nמידע זה יעזור לצוות ניהול הבניין להגיב ביעילות."
                : $"Hi {ticket.TenantName}, thank you for reporting this issue. " +
                  $"To help us address it quickly, could you please provide:\n" +
                  string.Join("\n", missingParts.Select((p, i) => $"{i + 1}. {p}")) +
                  "\n\nThis will help the building management team respond more efficiently.";
        }
        else
        {
            message = he
                ? $"שלום {ticket.TenantName}, תודה על הדיווח המפורט. הפנייה שלך התקבלה וצוות ניהול הבניין יבחן אותה בהקדם."
                : $"Hi {ticket.TenantName}, thank you for the detailed report. " +
                  "Your ticket has been received and the building management team will review it shortly.";
        }

        int? matchedGroupId = null;
        string? incidentTitle = null;
        // Cluster if same area + category (strong signal), or same category + word overlap
        var matchingTicket = openTicketsInBuilding.FirstOrDefault(t =>
            t.Id != ticket.Id &&
            t.Category == ticket.Category &&
            (t.Area == ticket.Area || HasWordOverlap(t.Description, ticket.Description)));

        if (matchingTicket != null)
        {
            matchedGroupId = matchingTicket.IncidentGroupId;
            incidentTitle = $"{ticket.Area} - {ticket.Category} issue";
            message += he
                ? $"\n\nלידיעתך: דיווח זה נראה קשור לפנייה קיימת (פנייה #{matchingTicket.Id}). קישרנו אותם יחד כדי שצוות הניהול יוכל לטפל בהם כאירוע אחד."
                : $"\n\nNote: This appears related to an existing report (Ticket #{matchingTicket.Id}). " +
                  "We've linked these together so the management team can address them as one incident.";
        }

        return Task.FromResult(new AgentAnalysisResult
        {
            Message = message,
            MatchedIncidentGroupId = matchedGroupId,
            IncidentTitle = incidentTitle
        });
    }

    public Task<AgentReplyResult> ProcessTenantReplyAsync(
        TicketContext ticket, List<MessageEntry> conversationHistory, CancellationToken ct = default)
    {
        _logger.LogInformation("[FakeAI] Processing reply on ticket #{Id}", ticket.Id);

        var lastTenantMsg = conversationHistory.LastOrDefault(m => m.SenderType == "Tenant")?.Text ?? "";
        var he = IsHebrew(lastTenantMsg) || IsHebrew(ticket.Description);

        if (lastTenantMsg.Length < 10)
            return Task.FromResult(new AgentReplyResult
            {
                Message = he
                    ? "תודה על התגובה. האם תוכל/י לפרט קצת יותר כדי שנוכל לסייע לך?"
                    : "Thank you for your response. Could you provide a bit more detail so we can assist you better?"
            });

        var fieldUpdates = TryExtractFieldUpdates(lastTenantMsg);
        var message = he
            ? "תודה על המידע הנוסף. צוות ניהול הבניין קיבל עדכון ויתחשב במידע זה בטיפול בפנייתך."
            : "Thank you for the additional information. The building management team has been notified " +
              "and will take this into account when handling your request.";

        if (fieldUpdates != null)
        {
            message += he
                ? "\n\nעדכנתי את פרטי הפנייה לפי המידע שסיפקת."
                : "\n\nI've updated the ticket details based on the information you provided.";
        }

        return Task.FromResult(new AgentReplyResult { Message = message, FieldUpdates = fieldUpdates });
    }

    private static TicketFieldUpdates? TryExtractFieldUpdates(string text)
    {
        var lower = text.ToLowerInvariant();
        string? area = null;
        string? category = null;

        var areaKeywords = new Dictionary<string, string>
        {
            ["stairwell"] = "Stairwell", ["חדר מדרגות"] = "Stairwell", ["מדרגות"] = "Stairwell",
            ["parking"] = "Parking", ["חניה"] = "Parking", ["חנייה"] = "Parking",
            ["lobby"] = "Lobby", ["לובי"] = "Lobby", ["כניסה"] = "Lobby",
            ["corridor"] = "Corridor", ["מסדרון"] = "Corridor",
            ["garbage"] = "GarbageRoom", ["אשפה"] = "GarbageRoom",
            ["garden"] = "Garden", ["גינה"] = "Garden", ["גן"] = "Garden",
            ["roof"] = "Roof", ["גג"] = "Roof",
        };
        foreach (var (kw, val) in areaKeywords)
        {
            if (lower.Contains(kw)) { area = val; break; }
        }

        var catKeywords = new Dictionary<string, string>
        {
            ["plumbing"] = "Plumbing", ["pipe"] = "Plumbing", ["leak"] = "Plumbing", ["water"] = "Plumbing",
            ["אינסטלציה"] = "Plumbing", ["צנרת"] = "Plumbing", ["נזילה"] = "Plumbing",
            ["electrical"] = "Electrical", ["power"] = "Electrical", ["light"] = "Electrical",
            ["חשמל"] = "Electrical", ["תאורה"] = "Electrical",
            ["hvac"] = "HVAC", ["air condition"] = "HVAC", ["heating"] = "HVAC",
            ["מיזוג"] = "HVAC", ["חימום"] = "HVAC",
            ["cleaning"] = "Cleaning", ["ניקיון"] = "Cleaning",
            ["pest"] = "Pest", ["מזיקים"] = "Pest", ["חרקים"] = "Pest",
            ["elevator"] = "Elevator", ["מעלית"] = "Elevator",
            ["security"] = "Security", ["אבטחה"] = "Security",
            ["structural"] = "Structural", ["crack"] = "Structural", ["סדק"] = "Structural",
        };
        foreach (var (kw, val) in catKeywords)
        {
            if (lower.Contains(kw)) { category = val; break; }
        }

        if (area == null && category == null) return null;
        return new TicketFieldUpdates { Area = area, Category = category };
    }

    public Task<string> GenerateResolutionFollowUpAsync(TicketContext ticket, CancellationToken ct = default)
    {
        _logger.LogInformation("[FakeAI] Generating resolution follow-up for ticket #{Id}", ticket.Id);

        var he = IsHebrew(ticket.Description);

        return Task.FromResult(he
            ? $"שלום {ticket.TenantName}, הפנייה שלך בנוגע לתקלת {ticket.Category.ToLowerInvariant()} " +
              $"באזור {ticket.Area.ToLowerInvariant()} סומנה כפתורה.\n\n" +
              "האם הבעיה נפתרה לשביעות רצונך? אנא השב/י כדי לעדכן אותנו, או פנה/י להנהלת הבניין אם נדרשת עזרה נוספת."
            : $"Hi {ticket.TenantName}, your ticket regarding the {ticket.Category.ToLowerInvariant()} " +
              $"issue in {ticket.Area.ToLowerInvariant()} has been marked as resolved.\n\n" +
              "Has this issue been resolved to your satisfaction? " +
              "Please reply to let us know, or contact the building management if you need further assistance.");
    }

    private static bool IsHebrew(string text) =>
        text.Any(c => c >= '\u0590' && c <= '\u05FF');

    private static bool ContainsLocationHint(string text) =>
        new[] { "floor", "apartment", "apt", "room", "entrance", "lobby", "parking", "roof", "garden", "stairwell", "corridor", "קומה", "דירה", "חדר", "כניסה" }
            .Any(text.Contains);

    private static bool ContainsTimeHint(string text) =>
        new[] { "since", "started", "yesterday", "today", "days", "weeks", "hours", "morning", "night", "ago", "מאז", "התחיל", "אתמול", "היום" }
            .Any(text.Contains);

    private static bool HasWordOverlap(string a, string b)
    {
        var wordsA = a.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length > 2).ToHashSet();
        var wordsB = b.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length > 2).ToHashSet();
        var overlap = wordsA.Intersect(wordsB).Count();
        return overlap >= 2;
    }
}
