using System.Collections.Concurrent;
using BuildingManagement.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace BuildingManagement.Infrastructure.Services.Email;

public record FakeEmailMessage(string To, string Subject, string Body, DateTime SentAt, string MessageId);

public class FakeEmailSender : IEmailSender
{
    private readonly ILogger<FakeEmailSender> _logger;
    private static readonly ConcurrentQueue<FakeEmailMessage> _sentMessages = new();
    private const int MaxStoredMessages = 200;

    public FakeEmailSender(ILogger<FakeEmailSender> logger) => _logger = logger;

    public string ProviderName => "Fake";

    public Task<EmailSendResult> SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
    {
        var msgId = $"FAKE-EMAIL-{Guid.NewGuid():N}";
        _logger.LogInformation("[FakeEmail] To: {To} | Subject: {Subject} | MsgId: {MsgId} | Body: {Body}",
            toEmail, subject, msgId, htmlBody);

        _sentMessages.Enqueue(new FakeEmailMessage(toEmail, subject, htmlBody, DateTime.UtcNow, msgId));
        while (_sentMessages.Count > MaxStoredMessages)
            _sentMessages.TryDequeue(out _);

        return Task.FromResult(new EmailSendResult(true, msgId));
    }

    public static IReadOnlyList<FakeEmailMessage> GetRecentMessages(int count = 50)
        => _sentMessages.Reverse().Take(count).ToList();
}
