using System;

#nullable enable

namespace Nocturne.Connectors.Core.Services
{
    /// <summary>
    /// Implements exponential backoff strategy for retry logic
    /// </summary>
    public class BackoffStrategy
    {
        private readonly int _baseIntervalMs;
        private readonly int _maxRetries;
        private readonly double _exponentialBase;
        private readonly int _maxDelayMs;
        private readonly bool _useJitter;
        private readonly Random _random;

        public BackoffStrategy(
            int baseIntervalMs = 1000,
            int maxRetries = 10,
            double exponentialBase = 2.0,
            int maxDelayMs = 300000, // 5 minutes
            bool useJitter = true
        )
        {
            _baseIntervalMs = baseIntervalMs;
            _maxRetries = maxRetries;
            _exponentialBase = exponentialBase;
            _maxDelayMs = maxDelayMs;
            _useJitter = useJitter;
            _random = new Random();
        }

        /// <summary>
        /// Calculate the delay for a given retry attempt
        /// </summary>
        /// <param name="attempt">The current retry attempt (0-based)</param>
        /// <returns>The delay timespan for this attempt</returns>
        public TimeSpan CalculateDelay(int attempt)
        {
            if (attempt >= _maxRetries)
            {
                return TimeSpan.FromMilliseconds(_maxDelayMs);
            }

            // Calculate exponential backoff
            var exponentialDelay = _baseIntervalMs * Math.Pow(_exponentialBase, attempt);

            // Cap at maximum delay
            var cappedDelay = Math.Min(exponentialDelay, _maxDelayMs);

            // Add jitter if enabled (up to 25% variance)
            if (_useJitter)
            {
                var jitterRange = cappedDelay * 0.25;
                var jitter = (_random.NextDouble() - 0.5) * 2 * jitterRange;
                cappedDelay = Math.Max(0, cappedDelay + jitter);
            }

            return TimeSpan.FromMilliseconds(cappedDelay);
        }

        /// <summary>
        /// Check if we should continue retrying
        /// </summary>
        /// <param name="attempt">Current retry attempt</param>
        /// <returns>True if should retry, false if max retries exceeded</returns>
        public bool ShouldRetry(int attempt)
        {
            return attempt < _maxRetries;
        }
    }
}
