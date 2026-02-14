namespace BuildingManagement.Core.Interfaces;

public record SmsSendResult(bool Success, string? MessageId = null, string? Error = null);

public interface ISmsSender
{
    string ProviderName { get; }
    Task<SmsSendResult> SendAsync(string toPhoneE164, string message, CancellationToken ct = default);
}
