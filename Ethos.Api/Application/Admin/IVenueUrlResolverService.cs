using System.Net;

namespace Ethos.Api.Application.Admin;

public sealed class ResolvedVenueDto
{
    public string VenueName { get; init; } = string.Empty;
    public string FullAddress { get; init; } = string.Empty;
    public string? City { get; init; }
    public string? Area { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public string? GooglePlaceId { get; init; }
    public string? LocationUrl { get; init; }
    public string ResolutionSource { get; init; } = string.Empty; // "google_places" | "url_metadata_fallback"
    public bool IsVerified { get; init; }
}

public interface IVenueUrlResolverService
{
    Task<ResolvedVenueDto> ResolveGoogleMapsUrlAsync(string rawUrl, CancellationToken cancellationToken = default);
    bool ValidateSsrfSafeHost(Uri uri);
    bool IsPrivateOrReservedIp(IPAddress ip);
}
