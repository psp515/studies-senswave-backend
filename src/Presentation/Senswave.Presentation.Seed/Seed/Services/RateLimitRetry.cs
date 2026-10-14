using System.Net;
using Refit;

namespace Senswave.Presentation.Seed.Seed.Services;

/// <summary>
/// Retries an API call that was rejected with 429 Too Many Requests, waiting for the
/// Retry-After header (or a default delay) between attempts. Other errors propagate.
/// </summary>
public static class RateLimitRetry
{
    private static readonly TimeSpan DefaultDelay = TimeSpan.FromSeconds(5);
    private const int MaxAttempts = 20;

    public static async Task<T> ExecuteAsync<T>(Func<Task<T>> action, ILogger logger, string operation, CancellationToken cancellationToken = default)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                return await action();
            }
            catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests && attempt < MaxAttempts)
            {
                var delay = ex.Headers.RetryAfter?.Delta ?? DefaultDelay;

                if (delay <= TimeSpan.Zero)
                    delay = DefaultDelay;

                logger.LogWarning("[{operation}] Rate limited (attempt {attempt}/{max}). Waiting {delay}s before retry.",
                    operation, attempt, MaxAttempts, delay.TotalSeconds);

                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    public static async Task ExecuteAsync(Func<Task> action, ILogger logger, string operation, CancellationToken cancellationToken = default)
    {
        await ExecuteAsync(async () =>
        {
            await action();
            return true;
        }, logger, operation, cancellationToken);
    }
}
