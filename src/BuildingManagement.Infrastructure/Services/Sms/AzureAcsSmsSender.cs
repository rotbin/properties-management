using BuildingManagement.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BuildingManagement.Infrastructure.Services.Sms;

/// <summary>
/// Skeleton Azure Communication Services SMS sender.
/// Requires NuGet: Azure.Communication.Sms
/// Set Sms__AzureAcs__ConnectionString and Sms__FromNumber in config/env.
/// </summary>
public class AzureAcsSmsSender : ISmsSender
{
    private readonly IConfiguration _config;
    private readonly ILogger<AzureAcsSmsSender> _logger;

    public AzureAcsSmsSender(IConfiguration config, ILogger<AzureAcsSmsSender> logger)
    {
        _config = config;
        _logger = logger;
    }

    public string ProviderName => "AzureAcs";

    public Task<SmsSendResult> SendAsync(string toPhoneE164, string message, CancellationToken ct = default)
    {
        var connectionString = _config["Sms:AzureAcs:ConnectionString"];
        var fromNumber = _config["Sms:FromNumber"];

        if (string.IsNullOrEmpty(connectionString) || string.IsNullOrEmpty(fromNumber))
        {
            _logger.LogError("Azure Communication Services SMS is not configured. Set Sms:AzureAcs:ConnectionString and Sms:FromNumber.");
            return Task.FromResult(new SmsSendResult(false, Error: "AzureACS not configured. Set Sms:AzureAcs:ConnectionString and Sms:FromNumber in app settings."));
        }

        // TODO: Implement actual Azure Communication Services SMS send:
        // var client = new SmsClient(connectionString);
        // var response = await client.SendAsync(fromNumber, toPhoneE164, message, cancellationToken: ct);
        // return new SmsSendResult(response.Value.Successful, response.Value.MessageId);

        _logger.LogWarning("[AzureACS] Send not yet implemented. Would send to {To}: {Body}", toPhoneE164, message);
        return Task.FromResult(new SmsSendResult(false, Error: "AzureACS send not yet implemented. Use Fake provider for development."));
    }
}
