using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Inventory.Services.Caching;

/// <summary>
/// Thin, testable wrapper over <see cref="IMemoryCache"/>.
/// <para>
/// It adds two things the raw cache lacks: single-flight population, so a cache
/// miss under load runs the factory once rather than once per request, and key
/// tracking, so a whole group of entries can be evicted when its underlying
/// master data changes.
/// </para>
/// </summary>
public interface ICacheService
{
    /// <summary>Returns the cached value, populating it with <paramref name="factory"/> on a miss.</summary>
    Task<T> GetOrCreateAsync<T>(
        string key,
        Func<Task<T>> factory,
        TimeSpan? duration = null,
        CancellationToken cancellationToken = default);

    /// <summary>Reads a cached value without populating it.</summary>
    bool TryGet<T>(string key, out T? value);

    /// <summary>Adds or replaces a value.</summary>
    void Set<T>(string key, T value, TimeSpan? duration = null);

    /// <summary>Removes one entry.</summary>
    void Remove(string key);

    /// <summary>Removes every entry whose key starts with <paramref name="prefix"/>.</summary>
    void RemoveByPrefix(string prefix);

    /// <summary>Clears every entry this service created.</summary>
    void Clear();
}

/// <inheritdoc cref="ICacheService" />
public sealed class MemoryCacheService : ICacheService, IDisposable
{
    private static readonly TimeSpan DefaultDuration = TimeSpan.FromMinutes(10);

    private readonly IMemoryCache _cache;
    private readonly ILogger<MemoryCacheService> _logger;

    /// <summary>Keys this service owns; <see cref="IMemoryCache"/> cannot enumerate its own.</summary>
    private readonly ConcurrentDictionary<string, byte> _keys = new();

    /// <summary>One semaphore per key gives single-flight population.</summary>
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    private bool _disposed;

    public MemoryCacheService(IMemoryCache cache, ILogger<MemoryCacheService> logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<Task<T>> factory,
        TimeSpan? duration = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(factory);

        if (_cache.TryGetValue(key, out T? cached) && cached is not null)
        {
            return cached;
        }

        var gate = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            // Re-check: another caller may have populated it while we waited.
            if (_cache.TryGetValue(key, out cached) && cached is not null)
            {
                return cached;
            }

            var value = await factory().ConfigureAwait(false);
            Set(key, value, duration);
            return value;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <inheritdoc />
    public bool TryGet<T>(string key, out T? value)
        => _cache.TryGetValue(key, out value);

    /// <inheritdoc />
    public void Set<T>(string key, T value, TimeSpan? duration = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = duration ?? DefaultDuration,
            Size = 1
        };

        options.RegisterPostEvictionCallback((evictedKey, _, _, _) =>
            _keys.TryRemove(evictedKey.ToString() ?? string.Empty, out _));

        _cache.Set(key, value, options);
        _keys.TryAdd(key, 0);
    }

    /// <inheritdoc />
    public void Remove(string key)
    {
        _cache.Remove(key);
        _keys.TryRemove(key, out _);
    }

    /// <inheritdoc />
    public void RemoveByPrefix(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return;
        }

        var matches = _keys.Keys.Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();

        foreach (var key in matches)
        {
            Remove(key);
        }

        _logger.LogDebug("Evicted {Count} cache entries with prefix {Prefix}.", matches.Count, prefix);
    }

    /// <inheritdoc />
    public void Clear()
    {
        foreach (var key in _keys.Keys.ToList())
        {
            Remove(key);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var gate in _locks.Values)
        {
            gate.Dispose();
        }

        _locks.Clear();
        _disposed = true;
    }
}
