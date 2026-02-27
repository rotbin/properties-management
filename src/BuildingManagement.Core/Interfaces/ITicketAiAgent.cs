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
    /// Process a tenant's reply in the ticket thread. Returns a follow-up message and
    /// optional field updates extracted from the conversation (area, category, priority, description, etc.).
    /// </summary>
    Task<AgentReplyResult> ProcessTenantReplyAsync(TicketContext ticket, List<MessageEntry> conversationHistory, CancellationToken ct = default);

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
    public TicketFieldUpdates? FieldUpdates { get; init; }
}

public record AgentReplyResult
{
    public string? Message { get; init; }
    public TicketFieldUpdates? FieldUpdates { get; init; }
    /// <summary>If set, the agent suggests changing the ticket status (e.g. "InReview" once data collection is complete).</summary>
    public string? SuggestedStatus { get; init; }
}

/// <summary>
/// Optional field updates the AI agent extracts from tenant replies.
/// Null properties mean "no change".
/// </summary>
public record TicketFieldUpdates
{
    public string? Area { get; init; }
    public string? Category { get; init; }
    public string? Priority { get; init; }
    public bool? IsEmergency { get; init; }
    public string? Description { get; init; }
}
