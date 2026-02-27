using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BuildingManagement.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BuildingManagement.Infrastructure.Services.Ai;

/// <summary>
/// AI agent powered by Azure OpenAI GPT-4o for ticket analysis.
/// Uses Azure OpenAI REST API format.
/// </summary>
public class OpenAiTicketAgent : ITicketAiAgent
{
    private readonly HttpClient _http;
    private readonly ILogger<OpenAiTicketAgent> _logger;
    private readonly string _endpoint;
    private readonly string _apiKey;
    private readonly string _deploymentName;
    private readonly string _apiVersion;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public OpenAiTicketAgent(HttpClient http, IConfiguration config, ILogger<OpenAiTicketAgent> logger)
    {
        _http = http;
        _logger = logger;
        _endpoint = config["OpenAi:Endpoint"]?.TrimEnd('/') ?? "";
        _apiKey = config["OpenAi:ApiKey"] ?? "";
        _deploymentName = config["OpenAi:DeploymentName"] ?? "gpt-4o-dataplatform";
        _apiVersion = config["OpenAi:ApiVersion"] ?? "2024-12-01-preview";
    }

    public async Task<AgentAnalysisResult> AnalyzeNewTicketAsync(
        TicketContext ticket, List<TicketSummary> openTicketsInBuilding, CancellationToken ct = default)
    {
        var openTicketsSummary = openTicketsInBuilding.Count > 0
            ? "Existing open tickets in this building:\n" + string.Join("\n", openTicketsInBuilding.Select(t =>
                $"- Ticket #{t.Id}: [{t.Area}/{t.Category}] {Truncate(t.Description, 100)}" +
                (t.IncidentGroupId.HasValue ? $" (incident group #{t.IncidentGroupId})" : "")))
            : "No other open tickets in this building.";

        var systemPrompt = """
            You are a building management AI assistant. Analyze the new maintenance ticket below.

            CRITICAL: Respond in the SAME LANGUAGE the tenant used in their description. If the description is in Hebrew, your message MUST be in Hebrew. If in English, respond in English.

            Your tasks:
            1. CHECK if the description is missing key details: exact location, when it started, severity/urgency, or access instructions (for in-unit issues). If details are missing, ask up to 2 concise clarifying questions.
            2. CHECK if this ticket appears similar to any existing open tickets in the same building (same type of issue, same area). If so, identify the matching ticket ID.
            3. Always be polite, professional, and concise. Address the tenant by name.

            Respond in this JSON format (no markdown):
            {
              "message": "Your message to the tenant (in the SAME language as their description)",
              "matchedTicketId": null or <number>,
              "incidentTitle": null or "short title for the grouped incident"
            }
            """;

        var userPrompt = $"""
            New Ticket #{ticket.Id}:
            - Building: {ticket.BuildingName}
            - Unit: {ticket.UnitNumber ?? "N/A"}
            - Area: {ticket.Area}
            - Category: {ticket.Category}
            - Priority: {ticket.Priority}
            - Emergency: {ticket.IsEmergency}
            - Tenant: {ticket.TenantName}
            - Description: {ticket.Description}

            {openTicketsSummary}
            """;

        var json = await CallAzureOpenAiAsync(systemPrompt, userPrompt, ct);
        if (json == null)
            return new AgentAnalysisResult { Message = null };

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var message = root.TryGetProperty("message", out var msgEl) ? msgEl.GetString() : null;
            int? matchedTicketId = root.TryGetProperty("matchedTicketId", out var mtEl) && mtEl.ValueKind == JsonValueKind.Number ? mtEl.GetInt32() : null;
            var incidentTitle = root.TryGetProperty("incidentTitle", out var itEl) ? itEl.GetString() : null;

            int? matchedGroupId = null;
            if (matchedTicketId.HasValue)
            {
                var matched = openTicketsInBuilding.FirstOrDefault(t => t.Id == matchedTicketId.Value);
                matchedGroupId = matched?.IncidentGroupId;
            }

            return new AgentAnalysisResult
            {
                Message = message,
                MatchedIncidentGroupId = matchedGroupId,
                IncidentTitle = incidentTitle
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse Azure OpenAI analysis response");
            return new AgentAnalysisResult { Message = json };
        }
    }

    public async Task<string?> ProcessTenantReplyAsync(
        TicketContext ticket, List<MessageEntry> conversationHistory, CancellationToken ct = default)
    {
        var systemPrompt = """
            You are a building management AI assistant. The tenant has replied to their maintenance ticket.
            Review the conversation and provide a helpful follow-up. Be concise and professional.
            If they've provided the information you asked for, thank them and confirm it's been noted.
            If the reply is unclear, ask one clarifying question.
            CRITICAL: Respond in the SAME LANGUAGE the tenant is using. If Hebrew, reply in Hebrew. If English, reply in English.
            Respond with plain text (not JSON).
            """;

        var convoText = string.Join("\n", conversationHistory.Select(m => $"[{m.SenderType}]: {m.Text}"));
        var userPrompt = $"Ticket #{ticket.Id} ({ticket.Category} in {ticket.Area}):\n{convoText}";

        return await CallAzureOpenAiAsync(systemPrompt, userPrompt, ct);
    }

    public async Task<string> GenerateResolutionFollowUpAsync(TicketContext ticket, CancellationToken ct = default)
    {
        var systemPrompt = """
            You are a building management AI assistant. A maintenance ticket has been marked as resolved.
            Write a short, polite message to the tenant asking if the issue was resolved to their satisfaction.
            Address them by name. Keep it to 2-3 sentences.
            CRITICAL: Respond in the SAME LANGUAGE the tenant used in their ticket description. If Hebrew, reply in Hebrew. If English, reply in English.
            Respond with plain text.
            """;

        var userPrompt = $"Ticket #{ticket.Id}: {ticket.Category} issue in {ticket.Area} for tenant {ticket.TenantName}. Description: {ticket.Description}";

        return await CallAzureOpenAiAsync(systemPrompt, userPrompt, ct)
            ?? $"Hi {ticket.TenantName}, your ticket has been resolved. Please let us know if the issue has been fully addressed.";
    }

    private async Task<string?> CallAzureOpenAiAsync(string systemPrompt, string userPrompt, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_apiKey) || string.IsNullOrEmpty(_endpoint))
        {
            _logger.LogWarning("Azure OpenAI endpoint or API key not configured");
            return null;
        }

        try
        {
            var url = $"{_endpoint}/openai/deployments/{_deploymentName}/chat/completions?api-version={_apiVersion}";

            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(new
                {
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = userPrompt }
                    },
                    max_tokens = 500,
                    temperature = 0.7
                })
            };
            request.Headers.Add("api-key", _apiKey);

            var response = await _http.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Azure OpenAI API error: {Status} {Body}", response.StatusCode, Truncate(body, 300));
                return null;
            }

            using var doc = JsonDocument.Parse(body);
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            _logger.LogInformation("Azure OpenAI response received ({Deployment})", _deploymentName);
            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Azure OpenAI API call failed");
            return null;
        }
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "...";
}
