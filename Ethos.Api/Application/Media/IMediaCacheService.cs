namespace Ethos.Api.Application.Media;

public interface IMediaCacheService
{
    Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null);
    void InvalidateAllMediaCache();
    string NormalizeCacheKey(string? section, string? category, string? mediaType);
}
