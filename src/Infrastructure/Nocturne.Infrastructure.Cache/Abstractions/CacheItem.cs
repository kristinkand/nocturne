namespace Nocturne.Infrastructure.Cache.Abstractions;

/// <summary>
/// Cache item with metadata
/// </summary>
/// <typeparam name="T">Type of cached data</typeparam>
public class CacheItem<T>
{
    /// <summary>
    /// The cached value
    /// </summary>
    public T Value { get; set; } = default!;

    /// <summary>
    /// When the item was cached
    /// </summary>
    public DateTimeOffset CachedAt { get; set; }

    /// <summary>
    /// When the item expires
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>
    /// Cache tags for invalidation
    /// </summary>
    public string[] Tags { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Creates a new cache item
    /// </summary>
    /// <param name="value">Value to cache</param>
    /// <param name="expiresAt">When the item expires</param>
    /// <param name="tags">Cache tags</param>
    public CacheItem(T value, DateTimeOffset? expiresAt = null, params string[] tags)
    {
        Value = value;
        CachedAt = DateTimeOffset.UtcNow;
        ExpiresAt = expiresAt;
        Tags = tags ?? Array.Empty<string>();
    }

    /// <summary>
    /// Creates a new cache item with TTL
    /// </summary>
    /// <param name="value">Value to cache</param>
    /// <param name="ttl">Time to live</param>
    /// <param name="tags">Cache tags</param>
    public CacheItem(T value, TimeSpan ttl, params string[] tags)
        : this(value, DateTimeOffset.UtcNow.Add(ttl), tags) { }
}
