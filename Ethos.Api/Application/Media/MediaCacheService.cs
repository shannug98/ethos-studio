using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace Ethos.Api.Application.Media;

public class MediaCacheService : IMediaCacheService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<MediaCacheService> _logger;
    private readonly ConcurrentDictionary<string, byte> _activeKeys = new(StringComparer.OrdinalIgnoreCase);

    public MediaCacheService(IMemoryCache cache, ILogger<MediaCacheService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public string NormalizeCacheKey(string? section, string? category, string? mediaType)
    {
        var s = string.IsNullOrWhiteSpace(section) || section.Equals("all", StringComparison.OrdinalIgnoreCase)
            ? "all"
            : section.Trim().ToLowerInvariant();

        var c = string.IsNullOrWhiteSpace(category) || category.Equals("all", StringComparison.OrdinalIgnoreCase)
            ? "all"
            : category.Trim().ToLowerInvariant();

        var m = string.IsNullOrWhiteSpace(mediaType) || mediaType.Equals("all", StringComparison.OrdinalIgnoreCase)
            ? "all"
            : mediaType.Trim().ToLowerInvariant();

        return $"media_public:{s}:{c}:{m}";
    }

    public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null)
    {
        if (_cache.TryGetValue(key, out T? cachedValue) && cachedValue != null)
        {
            return cachedValue;
        }

        var value = await factory();
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiration ?? TimeSpan.FromMinutes(10),
            SlidingExpiration = TimeSpan.FromMinutes(3)
        };

        _cache.Set(key, value, options);
        _activeKeys.TryAdd(key, 0);

        return value;
    }

    public void InvalidateAllMediaCache()
    {
        var count = _activeKeys.Count;
        foreach (var key in _activeKeys.Keys)
        {
            _cache.Remove(key);
        }
        _activeKeys.Clear();
        _logger.LogInformation("Invalidated {Count} public media cache entries.", count);
    }
}
