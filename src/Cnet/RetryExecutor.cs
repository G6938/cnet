using Telegram.Bot.Exceptions;

namespace Cnet;

public static class RetryExecutor
{
    public static async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> action,
        int maxAttempts,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxAttempts, 1);

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await action(cancellationToken).ConfigureAwait(false);
            }
            catch (ApiRequestException exception) when (
                attempt < maxAttempts && exception.Parameters?.RetryAfter is int retryAfter)
            {
                await Task.Delay(TimeSpan.FromSeconds(retryAfter), cancellationToken).ConfigureAwait(false);
            }
            catch (RequestException exception) when (
                attempt < maxAttempts && exception is not ApiRequestException)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt), cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException) when (attempt < maxAttempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public static async Task ExecuteAsync(
        Func<CancellationToken, Task> action,
        int maxAttempts,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        await ExecuteAsync(
            async ct =>
            {
                await action(ct).ConfigureAwait(false);
                return true;
            },
            maxAttempts,
            cancellationToken).ConfigureAwait(false);
    }
}
