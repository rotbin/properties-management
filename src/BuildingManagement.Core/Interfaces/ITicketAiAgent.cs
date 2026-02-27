namespace BuildingManagement.Core.Interfaces;

/// <summary>
/// AI agent that helps gather missing ticket details, clusters similar incidents,
/// and follows up on resolution satisfaction.
/// </summary>
public interface ITicketAiAgent
{
    /// <summary>
    /// Analyze a new ticket: check for missing details, look for similar open tickets to cluster.
    /// Returns one or more agent messages and an optional incident match.
    /// </summary>
    Task<AgentAnalysisResult> AnalyzeNewTicketAsync(TicketContext ticket, List<TicketSummary> openTicketsInBuilding, CancellationToken ct = default);

    /// <summary>
    /// Process a tenant's reply in the ticket thread and generate a follow-up agent response.
    /// </summary>
    Task<string?> ProcessTenantReplyAsync(TicketContext ticket, List<MessageEntry> conversationHistory, CancellationToken ct = default);

    /// <summary>
    /// Generate a satisfaction check message when a ticket is resolved.
    /// </summary>
    Task<string> GenerateResolutionFollowUpAsync(TicketContext ticket, CancellationToken ct = default);
}

public record TicketContext(
    int Id,
    string BuildingName,
    string? UnitNumber,
    string Area,
    string Category,
    string Priority,
    bool IsEmergency,
    string Description,
    string TenantName
);

public record TicketSummary(
    int Id,
    string Area,
    string Category,
    string Description,
    int? IncidentGroupId
);

public record MessageEntry(string SenderType, string Text);

public record AgentAnalysisResult
{
    public string? Message { get; init; }
    public int? MatchedIncidentGroupId { get; init; }
    public string? IncidentTitle { get; init; }
}
