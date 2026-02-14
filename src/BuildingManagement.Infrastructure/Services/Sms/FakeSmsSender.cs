using System.Collections.Concurrent;
using BuildingManagement.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace BuildingManagement.Infrastructure.Services.Sms;

public record FakeSmsMessage(string To, string Body, DateTime SentAt, string MessageId);

public class FakeSmsSender : ISmsSender
{
    private readonly ILogger<FakeSmsSender> _logger;
    private static readonly ConcurrentQueue<FakeSmsMessage> _sentMessages = new();
    private const int MaxStoredMessages = 200;

    public FakeSmsSender(ILogger<FakeSmsSender> logger) => _logger = logger;

    public string ProviderName => "Fake";

    public Task<SmsSendResult> SendAsync(string toPhoneE164, string message, CancellationToken ct = default)
    {
        var msgId = $"FAKE-{Guid.NewGuid():N}";
        _logger.LogInformation("[FakeSMS] To: {To} | MsgId: {MsgId} | Body: {Body}", toPhoneE164, msgId, message);

        _sentMessages.Enqueue(new FakeSmsMessage(toPhoneE164, message, DateTime.UtcNow, msgId));
        while (_sentMessages.Count > MaxStoredMessages)
            _sentMessages.TryDequeue(out _);

        return Task.FromResult(new SmsSendResult(true, msgId));
    }

    /// <summary>Get last N fake messages for debugging.</summary>
    public static IReadOnlyList<FakeSmsMessage> GetRecentMessages(int count = 50)
        => _sentMessages.Reverse().Take(count).ToList();
}
