using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Nocturne.Connectors.Core.Interfaces;
using Nocturne.Connectors.Core.Models;

namespace Nocturne.Connectors.Core.Services
{
    /// <summary>
    /// Thread-safe implementation of IConnectorMetricsTracker with per-data-type tracking
    /// </summary>
    public class ConnectorMetricsTracker : IConnectorMetricsTracker
    {
        private long _lastEntryTicks = 0; // 0 indicates null/not set
        private long _lastSyncTicks = 0; // 0 indicates null/not set

        // Per-type total counts
        private readonly ConcurrentDictionary<SyncDataType, long> _totalItemsByType = new();

        // Per-type hourly buckets for 24h sliding window
        // Key is (SyncDataType, hour key), Value is count for that hour
        private readonly ConcurrentDictionary<(SyncDataType Type, long HourKey), int> _hourlyBucketsByType = new();

        // Store recent entry timestamps (limited to 50 most recent)
        private readonly ConcurrentQueue<DateTime> _recentTimestamps = new();
        private const int MaxRecentTimestamps = 50;

        public long TotalEntries => _totalItemsByType.Values.Sum();

        public DateTime? LastEntryTime
        {
            get
            {
                var ticks = Interlocked.Read(ref _lastEntryTicks);
                return ticks == 0 ? null : new DateTime(ticks, DateTimeKind.Utc);
            }
        }

        public int EntriesLast24Hours
        {
            get
            {
                CleanOldBuckets();
                return _hourlyBucketsByType.Values.Sum();
            }
        }

        public DateTime? LastSyncTime
        {
            get
            {
                var ticks = Interlocked.Read(ref _lastSyncTicks);
                return ticks == 0 ? null : new DateTime(ticks, DateTimeKind.Utc);
            }
        }

        public void TrackEntries(int count, DateTime? latestTimestamp = null)
        {
            // Legacy method - track as Glucose for backward compatibility
            TrackItems(SyncDataType.Glucose, count, latestTimestamp);
        }

        public void TrackItems(SyncDataType dataType, int count, DateTime? latestTimestamp = null)
        {
            if (count <= 0) return;

            // Update per-type total
            _totalItemsByType.AddOrUpdate(dataType, count, (_, existing) => existing + count);

            var timestampToUse = latestTimestamp?.ToUniversalTime() ?? DateTime.UtcNow;

            UpdateLastEntryTime(timestampToUse);

            // Add to recent timestamps queue
            _recentTimestamps.Enqueue(timestampToUse);

            // Trim queue if it exceeds max size
            while (_recentTimestamps.Count > MaxRecentTimestamps)
            {
                _recentTimestamps.TryDequeue(out _);
            }

            // Add to the bucket for the current hour and data type
            var currentHourKey = GetHourKey(DateTime.UtcNow);
            var bucketKey = (dataType, currentHourKey);
            _hourlyBucketsByType.AddOrUpdate(bucketKey, count, (_, v) => v + count);

            // Cleanup occasionally
            CleanOldBuckets();
        }

        public long GetTotalItems(SyncDataType dataType)
        {
            return _totalItemsByType.TryGetValue(dataType, out var count) ? count : 0;
        }

        public int GetItemsLast24Hours(SyncDataType dataType)
        {
            CleanOldBuckets();
            var cutoffKey = GetHourKey(DateTime.UtcNow.AddHours(-24));

            return _hourlyBucketsByType
                .Where(kvp => kvp.Key.Type == dataType && kvp.Key.HourKey >= cutoffKey)
                .Sum(kvp => kvp.Value);
        }

        public Dictionary<SyncDataType, long> GetTotalItemsBreakdown()
        {
            return new Dictionary<SyncDataType, long>(_totalItemsByType);
        }

        public Dictionary<SyncDataType, int> GetItemsLast24HoursBreakdown()
        {
            CleanOldBuckets();
            var cutoffKey = GetHourKey(DateTime.UtcNow.AddHours(-24));

            return _hourlyBucketsByType
                .Where(kvp => kvp.Key.HourKey >= cutoffKey)
                .GroupBy(kvp => kvp.Key.Type)
                .ToDictionary(g => g.Key, g => g.Sum(kvp => kvp.Value));
        }

        public DateTime[] GetRecentEntryTimestamps(int count)
        {
            return _recentTimestamps
                .OrderByDescending(t => t)
                .Take(count)
                .ToArray();
        }

        public void TrackSync()
        {
            var now = DateTime.UtcNow;
            Interlocked.Exchange(ref _lastSyncTicks, now.Ticks);
        }

        private void UpdateLastEntryTime(DateTime timestamp)
        {
            var newTicks = timestamp.ToUniversalTime().Ticks;
            var currentTicks = Interlocked.Read(ref _lastEntryTicks);

            while (newTicks > currentTicks)
            {
                var original = Interlocked.CompareExchange(ref _lastEntryTicks, newTicks, currentTicks);
                if (original == currentTicks)
                {
                    break;
                }
                currentTicks = original;
            }
        }

        public void Reset()
        {
            _totalItemsByType.Clear();
            Interlocked.Exchange(ref _lastEntryTicks, 0);
            Interlocked.Exchange(ref _lastSyncTicks, 0);
            _hourlyBucketsByType.Clear();
            _recentTimestamps.Clear();
        }

        private void CleanOldBuckets()
        {
            var cutoffKey = GetHourKey(DateTime.UtcNow.AddHours(-24));

            // Remove keys older than cutoff
            foreach (var key in _hourlyBucketsByType.Keys)
            {
                if (key.HourKey < cutoffKey)
                {
                    _hourlyBucketsByType.TryRemove(key, out _);
                }
            }
        }

        private long GetHourKey(DateTime timestamp)
        {
            // Simple integer key: Total hours since epoch
            return (long)(timestamp - DateTime.UnixEpoch).TotalHours;
        }
    }
}
