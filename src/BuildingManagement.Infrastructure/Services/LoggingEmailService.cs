using BuildingManagement.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace BuildingManagement.Infrastructure.Services;

public class LoggingEmailService : IEmailService
{
    private readonly ILogger<LoggingEmailService> _logger;

    public LoggingEmailService(ILogger<LoggingEmailService> logger)
    {
        _logger = logger;
    }

    public Task SendEmailAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        _logger.LogInformation("EMAIL [to: {To}] Subject: {Subject} Body: {Body}", to, subject, body.Length > 200 ? body[..200] + "..." : body);
        return Task.CompletedTask;
    }
}
