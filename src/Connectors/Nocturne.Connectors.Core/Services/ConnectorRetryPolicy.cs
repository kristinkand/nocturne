using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Nocturne.Connectors.Core.Services;

/// <summary>
/// Retry policy for connector operations with exponential backoff
/// </summary>
public class ConnectorRetryPolicy
{
    private readonly ILogger<ConnectorRetryPolicy>? _logger;

    public ConnectorRetryPolicy(ILogger<ConnectorRetryPolicy>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Executes an operation with retry logic and exponential backoff
    /// </summary>
    /// <typeparam name="T">Return type</typeparam>
    /// <param name="operation">Operation to execute</param>
    /// <param name="maxAttempts">Maximum retry attempts</param>
    /// <param name="baseDelay">Base delay for exponential backoff</param>
    /// <returns>Result of the operation</returns>
    public async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation,
        int maxAttempts = 3,
        TimeSpan? baseDelay = null
    )
    {
        var delay = baseDelay ?? TimeSpan.FromSeconds(2);
        Exception? lastException = null;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (attempt < maxAttempts && IsRetriableException(ex))
            {
                lastException = ex;
                var waitTime = TimeSpan.FromMilliseconds(
                    delay.TotalMilliseconds * Math.Pow(2, attempt - 1)
                );

                _logger?.LogWarning(
                    ex,
                    "Attempt {Attempt}/{MaxAttempts} failed, retrying in {Delay}ms",
                    attempt,
                    maxAttempts,
                    waitTime.TotalMilliseconds
                );

                await Task.Delay(waitTime);
            }
        }

        // Final attempt without catch - let the exception bubble up
        try
        {
            return await operation();
        }
        catch (Exception ex)
        {
            _logger?.LogError(
                ex,
                "All {MaxAttempts} attempts failed, final exception",
                maxAttempts
            );
            throw;
        }
    }

    /// <summary>
    /// Determines if an exception is retriable
    /// </summary>
    private static bool IsRetriableException(Exception exception)
    {
        return exception switch
        {
            HttpRequestException => true,
            TaskCanceledException => true,
            TimeoutException => true,
            System.Net.Sockets.SocketException => true,
            _ => false,
        };
    }
}
