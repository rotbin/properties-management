namespace BuildingManagement.Infrastructure.Services.Sms;

/// <summary>
/// Simple in-process rate limiter for SMS sends (sliding window per minute).
/// </summary>
public class SmsRateLimiter
{
    private readonly int _maxPerMinute;
    private readonly Queue<DateTime> _timestamps = new();
    private readonly object _lock = new();

    public SmsRateLimiter(int maxPerMinute = 30)
    {
        _maxPerMinute = maxPerMinute;
    }

    /// <summary>
    /// Wait until a send slot is available. Returns true when ready.
    /// </summary>
    public async Task WaitForSlotAsync(CancellationToken ct = default)
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested();

            lock (_lock)
            {
                var now = DateTime.UtcNow;
                var cutoff = now.AddMinutes(-1);

                // Remove old timestamps
                while (_timestamps.Count > 0 && _timestamps.Peek() < cutoff)
                    _timestamps.Dequeue();

                if (_timestamps.Count < _maxPerMinute)
                {
                    _timestamps.Enqueue(now);
                    return;
                }
            }

            // Wait a bit before retrying
            await Task.Delay(500, ct);
        }
    }
}
