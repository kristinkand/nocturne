using System;
using System.Collections.Generic;
using Nocturne.Connectors.Core.Models;

namespace Nocturne.Connectors.Core.Interfaces
{
    /// <summary>
    /// tracks metrics for a connector service to be exposed via health checks
    /// </summary>
    public interface IConnectorMetricsTracker
    {
        /// <summary>
        /// Total number of entries processed since the service started
        /// </summary>
        long TotalEntries { get; }

        /// <summary>
        /// Timestamp of the last processed entry (UTC)
        /// </summary>
        DateTime? LastEntryTime { get; }

        /// <summary>
        /// Number of entries processed in the last 24 hours
        /// </summary>
        int EntriesLast24Hours { get; }

        /// <summary>
        /// Timestamp of the last sync operation (UTC)
        /// </summary>
        DateTime? LastSyncTime { get; }

        /// <summary>
        /// Records newly processed entries (legacy method, calls TrackItems with Glucose type)
        /// </summary>
        /// <param name="count">Number of entries processed</param>
        /// <param name="latestTimestamp">Timestamp of the latest entry if available</param>
        void TrackEntries(int count, DateTime? latestTimestamp = null);

        /// <summary>
        /// Records newly processed items of a specific type
        /// </summary>
        /// <param name="dataType">The type of data being tracked</param>
        /// <param name="count">Number of items processed</param>
        /// <param name="latestTimestamp">Timestamp of the latest item if available</param>
        void TrackItems(SyncDataType dataType, int count, DateTime? latestTimestamp = null);

        /// <summary>
        /// Gets the total items processed for a specific data type
        /// </summary>
        long GetTotalItems(SyncDataType dataType);

        /// <summary>
        /// Gets the items processed in the last 24 hours for a specific data type
        /// </summary>
        int GetItemsLast24Hours(SyncDataType dataType);

        /// <summary>
        /// Gets the breakdown of total items by data type
        /// </summary>
        Dictionary<SyncDataType, long> GetTotalItemsBreakdown();

        /// <summary>
        /// Gets the breakdown of items processed in the last 24 hours by data type
        /// </summary>
        Dictionary<SyncDataType, int> GetItemsLast24HoursBreakdown();

        /// <summary>
        /// Gets the most recent entry timestamps for health reporting
        /// </summary>
        /// <param name="count">Number of recent timestamps to retrieve</param>
        /// <returns>Array of recent entry timestamps in descending order (newest first)</returns>
        DateTime[] GetRecentEntryTimestamps(int count);

        /// <summary>
        /// Records a sync operation
        /// </summary>
        void TrackSync();

        /// <summary>
        /// Resets all metrics
        /// </summary>
        void Reset();
    }
}

