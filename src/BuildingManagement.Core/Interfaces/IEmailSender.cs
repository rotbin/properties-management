namespace BuildingManagement.Core.Interfaces;

public record EmailSendResult(bool Success, string? MessageId = null, string? Error = null);

public interface IEmailSender
{
    string ProviderName { get; }
    Task<EmailSendResult> SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default);
}
