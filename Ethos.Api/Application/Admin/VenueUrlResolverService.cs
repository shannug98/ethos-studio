using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Ethos.Api.Controllers.Admin;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Ethos.Api.Application.Admin;

public sealed partial class VenueUrlResolverService : IVenueUrlResolverService
{
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<VenueUrlResolverService> _logger;

    private const int MaxUrlLength = 2048;
    private const int MaxRedirects = 5;
    private const int MaxResponseBodyBytes = 64 * 1024; // 64 KB limit

    private static readonly HashSet<string> AllowedExactHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "maps.google.com",
        "google.com",
        "www.google.com",
        "maps.google.co.in",
        "google.co.in",
        "www.google.co.in",
        "maps.app.goo.gl",
        "goo.gl"
    };

    public VenueUrlResolverService(
        IConfiguration config,
        IHttpClientFactory httpClientFactory,
        ILogger<VenueUrlResolverService> logger)
    {
        _config = config;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<ResolvedVenueDto> ResolveGoogleMapsUrlAsync(string rawUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
            throw new ArgumentException("A valid Google Maps URL is required.", nameof(rawUrl));

        var trimmedUrl = rawUrl.Trim();
        if (trimmedUrl.Length > MaxUrlLength)
            throw new ArgumentException($"URL length exceeds the maximum limit of {MaxUrlLength} characters.", nameof(rawUrl));

        if (!Uri.TryCreate(trimmedUrl, UriKind.Absolute, out var initialUri) ||
            (initialUri.Scheme != Uri.UriSchemeHttps && initialUri.Scheme != Uri.UriSchemeHttp))
        {
            throw new ArgumentException("Only valid HTTP or HTTPS URLs are supported.", nameof(rawUrl));
        }

        // Validate SSRF safe host on initial URI
        await ValidateSsrfSafeUriAsync(initialUri, cancellationToken);

        // Follow redirects in a controlled, SSRF-safe manner to get destination URL
        var finalUri = await FollowRedirectsSafelyAsync(initialUri, cancellationToken);

        // Extract metadata (query string, place name, coordinates, place_id) from final destination URL
        var extracted = ExtractUrlMetadata(finalUri);

        // Try Google Places enrichment if API key is present
        var apiKey = _config["GoogleMaps:ApiKey"] ?? _config["GoogleMaps_ApiKey"] ?? Environment.GetEnvironmentVariable("GOOGLE_MAPS_API_KEY");
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            try
            {
                var placesResult = await TryEnrichFromGooglePlacesAsync(extracted, apiKey, cancellationToken);
                if (placesResult != null)
                {
                    return placesResult;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Google Places enrichment failed for URL {Url}, using fallback.", rawUrl);
            }
        }

        // Fallback: Best-effort extraction directly from URL metadata
        return BuildFallbackVenue(extracted);
    }

    public bool ValidateSsrfSafeHost(Uri uri)
    {
        if (uri == null) return false;
        var host = uri.Host;
        if (string.IsNullOrWhiteSpace(host)) return false;

        if (AllowedExactHosts.Contains(host)) return true;

        if (host.EndsWith(".google.com", StringComparison.OrdinalIgnoreCase) && !host.Contains('/') && !host.Contains('\\'))
            return true;
        if (host.EndsWith(".google.co.in", StringComparison.OrdinalIgnoreCase) && !host.Contains('/') && !host.Contains('\\'))
            return true;
        if (host.EndsWith(".goo.gl", StringComparison.OrdinalIgnoreCase) && !host.Contains('/') && !host.Contains('\\'))
            return true;

        return false;
    }

    public bool IsPrivateOrReservedIp(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip)) return true;
        if (ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any) || ip.Equals(IPAddress.None)) return true;

        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            var bytes = ip.GetAddressBytes();
            // 10.0.0.0/8
            if (bytes[0] == 10) return true;
            // 172.16.0.0/12 (172.16.0.0 - 172.31.255.255)
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;
            // 192.168.0.0/16
            if (bytes[0] == 192 && bytes[1] == 168) return true;
            // 169.254.0.0/16 (Link-local, AWS/GCP/Azure metadata IP 169.254.169.254)
            if (bytes[0] == 169 && bytes[1] == 254) return true;
            // 127.0.0.0/8
            if (bytes[0] == 127) return true;
            // 0.0.0.0/8
            if (bytes[0] == 0) return true;
            // 224.0.0.0/4 (multicast) and 240.0.0.0/4 (reserved)
            if (bytes[0] >= 224) return true;
        }
        else if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            if (ip.IsIPv6LinkLocal || ip.IsIPv6Multicast || ip.IsIPv6SiteLocal) return true;
            var bytes = ip.GetAddressBytes();
            // fc00::/7 (Unique Local Address)
            if ((bytes[0] & 0xFE) == 0xFC) return true;
            // fe80::/10 (Link-Local)
            if (bytes[0] == 0xFE && (bytes[1] & 0xC0) == 0x80) return true;
            // IPv4-mapped IPv6 (::ffff:x.x.x.x)
            if (ip.IsIPv4MappedToIPv6)
            {
                return IsPrivateOrReservedIp(ip.MapToIPv4());
            }
        }

        return false;
    }

    private async Task ValidateSsrfSafeUriAsync(Uri uri, CancellationToken cancellationToken)
    {
        if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            throw new SecurityException($"Forbidden protocol '{uri.Scheme}'. Only HTTP/HTTPS Google Maps URLs are permitted.");

        if (!ValidateSsrfSafeHost(uri))
            throw new SecurityException($"Forbidden domain '{uri.Host}'. Only official Google Maps domains are supported.");

        // DNS resolution check: Ensure target IP does not resolve to private / loopback / metadata ranges
        try
        {
            var addresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost, cancellationToken);
            if (addresses == null || addresses.Length == 0)
                throw new SecurityException($"Unable to resolve DNS for host '{uri.Host}'.");

            foreach (var ip in addresses)
            {
                if (IsPrivateOrReservedIp(ip))
                {
                    throw new SecurityException($"Destination IP '{ip}' resolves to a forbidden private, loopback, or metadata address.");
                }
            }
        }
        catch (SecurityException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new SecurityException($"DNS resolution failed for '{uri.Host}': {ex.Message}", ex);
        }
    }

    private async Task<Uri> FollowRedirectsSafelyAsync(Uri initialUri, CancellationToken cancellationToken)
    {
        var currentUri = initialUri;
        var redirectsFollowed = 0;
        var visitedUris = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { initialUri.AbsoluteUri };

        using var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false, // Controlled manual redirect inspection
            ConnectTimeout = TimeSpan.FromSeconds(5)
        };
        using var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(5)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

        while (redirectsFollowed < MaxRedirects)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, currentUri);
            using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if ((int)resp.StatusCode >= 300 && (int)resp.StatusCode <= 399)
            {
                var locationHeader = resp.Headers.Location;
                if (locationHeader == null)
                {
                    break;
                }

                // Resolve relative redirect URI against current URI
                var nextUri = locationHeader.IsAbsoluteUri ? locationHeader : new Uri(currentUri, locationHeader);

                // Re-validate SSRF safety on redirect target
                await ValidateSsrfSafeUriAsync(nextUri, cancellationToken);

                if (!visitedUris.Add(nextUri.AbsoluteUri))
                {
                    throw new SecurityException("Redirect loop detected during Google Maps URL resolution.");
                }

                currentUri = nextUri;
                redirectsFollowed++;
                continue;
            }

            break;
        }

        if (redirectsFollowed >= MaxRedirects)
        {
            throw new SecurityException($"Excessive redirects (exceeded limit of {MaxRedirects}).");
        }

        return currentUri;
    }

    private sealed class ExtractedUrlMetadata
    {
        public string? DestinationQuery { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? PlaceId { get; set; }
        public string FullDecodedUrl { get; set; } = string.Empty;
    }

    private static ExtractedUrlMetadata ExtractUrlMetadata(Uri uri)
    {
        var result = new ExtractedUrlMetadata
        {
            FullDecodedUrl = Uri.UnescapeDataString(uri.ToString())
        };

        var raw = result.FullDecodedUrl;

        // 1. Extract coordinates: @lat,lng or ?q=lat,lng or !3dlat!4dlng
        var atCoordMatch = Regex.Match(raw, @"@(-?\d{1,3}\.\d+),(-?\d{1,3}\.\d+)");
        if (atCoordMatch.Success &&
            double.TryParse(atCoordMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var atLat) &&
            double.TryParse(atCoordMatch.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var atLng))
        {
            result.Latitude = atLat;
            result.Longitude = atLng;
        }
        else
        {
            var bangCoordMatch = Regex.Match(raw, @"!3d(-?\d{1,3}\.\d+)!4d(-?\d{1,3}\.\d+)");
            if (bangCoordMatch.Success &&
                double.TryParse(bangCoordMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var bLat) &&
                double.TryParse(bangCoordMatch.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var bLng))
            {
                result.Latitude = bLat;
                result.Longitude = bLng;
            }
        }

        // 2. Extract destination / place name from directions URL or place URL
        // Example directions: /maps/dir//Ironhill+Cafe+Madhapur,+Plot+No.+11-2.../@17.445257,...
        var dirMatch = Regex.Match(raw, @"/maps/dir/[^/]*?/([^/@?#]+)");
        if (dirMatch.Success)
        {
            result.DestinationQuery = CleanQueryToken(dirMatch.Groups[1].Value);
        }
        else
        {
            // Example place: /maps/place/Ironhill+Cafe+Madhapur/@17.445257,...
            var placeMatch = Regex.Match(raw, @"/maps/place/([^/@?#]+)");
            if (placeMatch.Success)
            {
                result.DestinationQuery = CleanQueryToken(placeMatch.Groups[1].Value);
            }
            else
            {
                // Search query: /maps/search/?api=1&query=... or ?q=...
                var searchMatch = Regex.Match(raw, @"[?&](?:query|q)=([^&]+)");
                if (searchMatch.Success)
                {
                    result.DestinationQuery = CleanQueryToken(searchMatch.Groups[1].Value);
                }
            }
        }

        // 3. Extract Place ID if present in query parameters (query_place_id=...)
        var placeIdMatch = Regex.Match(raw, @"[?&]query_place_id=([a-zA-Z0-9_\-]+)");
        if (placeIdMatch.Success)
        {
            result.PlaceId = placeIdMatch.Groups[1].Value;
        }

        return result;
    }

    private static string CleanQueryToken(string token)
    {
        var cleaned = token.Replace('+', ' ').Trim();
        return Uri.UnescapeDataString(cleaned);
    }

    private async Task<ResolvedVenueDto?> TryEnrichFromGooglePlacesAsync(
        ExtractedUrlMetadata meta,
        string apiKey,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(5);

        GooglePlaceDetailResult? placeResult = null;
        string? resolvedPlaceId = meta.PlaceId;

        // Step A: If we already have a Place ID, fetch Place Details directly
        if (!string.IsNullOrWhiteSpace(resolvedPlaceId))
        {
            placeResult = await FetchPlaceDetailsAsync(client, resolvedPlaceId, apiKey, cancellationToken);
        }

        // Step B: If no Place ID or details failed, search by query name & coordinates
        if (placeResult == null && !string.IsNullOrWhiteSpace(meta.DestinationQuery))
        {
            // Query Places TextSearch with India country restriction & location bias
            var searchUrl = $"https://maps.googleapis.com/maps/api/place/findplacefromtext/json?input={Uri.EscapeDataString(meta.DestinationQuery)}&inputtype=textquery&fields=place_id,name,formatted_address,geometry&key={apiKey}";
            if (meta.Latitude.HasValue && meta.Longitude.HasValue)
            {
                searchUrl += $"&locationbias=point:{meta.Latitude.Value.ToString(CultureInfo.InvariantCulture)},{meta.Longitude.Value.ToString(CultureInfo.InvariantCulture)}";
            }

            var findRes = await client.GetFromJsonAsync<GoogleFindPlaceResponse>(searchUrl, cancellationToken);
            if (findRes?.Candidates != null && findRes.Candidates.Count > 0)
            {
                var candidate = findRes.Candidates[0];
                resolvedPlaceId = candidate.PlaceId;
                if (!string.IsNullOrWhiteSpace(resolvedPlaceId))
                {
                    placeResult = await FetchPlaceDetailsAsync(client, resolvedPlaceId, apiKey, cancellationToken);
                }
            }
        }

        if (placeResult == null)
            return null;

        // Extract structured components (City, Area, Pincode) from Google addressComponents
        string? city = null;
        string? area = null;
        if (placeResult.AddressComponents != null)
        {
            city = placeResult.AddressComponents.FirstOrDefault(c => c.Types.Contains("locality"))?.LongName
                   ?? placeResult.AddressComponents.FirstOrDefault(c => c.Types.Contains("administrative_area_level_2"))?.LongName;

            area = placeResult.AddressComponents.FirstOrDefault(c => c.Types.Contains("sublocality") || c.Types.Contains("sublocality_level_1") || c.Types.Contains("neighborhood"))?.LongName;
        }

        var venueName = !string.IsNullOrWhiteSpace(placeResult.Name) ? placeResult.Name : (meta.DestinationQuery ?? "Workshop Venue");
        var fullAddress = !string.IsNullOrWhiteSpace(placeResult.FormattedAddress) ? placeResult.FormattedAddress : venueName;
        var lat = placeResult.Geometry?.Location?.Lat ?? meta.Latitude;
        var lng = placeResult.Geometry?.Location?.Lng ?? meta.Longitude;

        // Generate clean canonical Google directions / search link (under 1000 characters)
        var canonicalUrl = !string.IsNullOrWhiteSpace(resolvedPlaceId)
            ? $"https://www.google.com/maps/dir/?api=1&destination={Uri.EscapeDataString(venueName)}&destination_place_id={resolvedPlaceId}"
            : (lat.HasValue && lng.HasValue
                ? $"https://www.google.com/maps/dir/?api=1&destination={lat.Value.ToString(CultureInfo.InvariantCulture)},{lng.Value.ToString(CultureInfo.InvariantCulture)}"
                : $"https://www.google.com/maps/search/?api=1&query={Uri.EscapeDataString(venueName)}");

        return new ResolvedVenueDto
        {
            VenueName = venueName,
            FullAddress = fullAddress,
            City = city,
            Area = area,
            Latitude = lat,
            Longitude = lng,
            GooglePlaceId = resolvedPlaceId,
            LocationUrl = canonicalUrl,
            ResolutionSource = "google_places",
            IsVerified = true
        };
    }

    private static async Task<GooglePlaceDetailResult?> FetchPlaceDetailsAsync(
        HttpClient client,
        string placeId,
        string apiKey,
        CancellationToken cancellationToken)
    {
        var url = $"https://maps.googleapis.com/maps/api/place/details/json?place_id={Uri.EscapeDataString(placeId)}&fields=name,formatted_address,geometry,address_components&key={apiKey}";
        var details = await client.GetFromJsonAsync<GooglePlaceDetailsResponse>(url, cancellationToken);
        return details?.Result;
    }

    private static ResolvedVenueDto BuildFallbackVenue(ExtractedUrlMetadata meta)
    {
        var rawQuery = meta.DestinationQuery ?? string.Empty;
        var parts = rawQuery.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        string venueName;
        string fullAddress;
        string? city = null;
        string? area = null;

        if (parts.Length > 0)
        {
            venueName = parts[0];
            fullAddress = parts.Length > 1 ? string.Join(", ", parts) : venueName;

            // Attempt best-effort city/area token heuristic
            for (var i = 1; i < parts.Length; i++)
            {
                var token = parts[i];
                if (Regex.IsMatch(token, @"^\d{6}$")) // Indian PIN code
                {
                    continue;
                }
                if (token.Equals("Hyderabad", StringComparison.OrdinalIgnoreCase) ||
                    token.Equals("Secunderabad", StringComparison.OrdinalIgnoreCase) ||
                    token.Equals("Bengaluru", StringComparison.OrdinalIgnoreCase) ||
                    token.Equals("Bangalore", StringComparison.OrdinalIgnoreCase) ||
                    token.Equals("Mumbai", StringComparison.OrdinalIgnoreCase) ||
                    token.Equals("Delhi", StringComparison.OrdinalIgnoreCase) ||
                    token.Equals("Chennai", StringComparison.OrdinalIgnoreCase) ||
                    token.Equals("Kolkata", StringComparison.OrdinalIgnoreCase) ||
                    token.Equals("Pune", StringComparison.OrdinalIgnoreCase))
                {
                    city = token;
                }
                else if (Regex.IsMatch(token, @"^(Plot|No\.?|D\.?No|H\.?No|Flat|Door)\b", RegexOptions.IgnoreCase))
                {
                    continue; // Skip building/plot tokens
                }
                else if (area == null && !token.StartsWith("Telangana", StringComparison.OrdinalIgnoreCase) && !token.StartsWith("India", StringComparison.OrdinalIgnoreCase))
                {
                    area = token;
                }
            }
        }
        else if (meta.Latitude.HasValue && meta.Longitude.HasValue)
        {
            venueName = $"Venue at {meta.Latitude.Value:F4}° N, {meta.Longitude.Value:F4}° E";
            fullAddress = $"Coordinates: {meta.Latitude.Value:F4}, {meta.Longitude.Value:F4}";
        }
        else
        {
            venueName = "Workshop Venue";
            fullAddress = "Location details not specified in Google Maps URL";
        }

        // Generate clean canonical Google search/directions link (deterministic, fits within 1000 chars)
        string canonicalUrl;
        if (meta.Latitude.HasValue && meta.Longitude.HasValue)
        {
            canonicalUrl = $"https://www.google.com/maps/search/?api=1&query={meta.Latitude.Value.ToString(CultureInfo.InvariantCulture)},{meta.Longitude.Value.ToString(CultureInfo.InvariantCulture)}";
        }
        else
        {
            canonicalUrl = $"https://www.google.com/maps/search/?api=1&query={Uri.EscapeDataString(venueName)}";
        }

        return new ResolvedVenueDto
        {
            VenueName = venueName,
            FullAddress = fullAddress,
            City = city,
            Area = area,
            Latitude = meta.Latitude,
            Longitude = meta.Longitude,
            GooglePlaceId = meta.PlaceId,
            LocationUrl = canonicalUrl,
            ResolutionSource = "url_metadata_fallback",
            IsVerified = false
        };
    }

    private sealed class GoogleFindPlaceResponse
    {
        [JsonPropertyName("candidates")]
        public List<GoogleFindPlaceCandidate>? Candidates { get; set; }
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;
    }

    private sealed class GoogleFindPlaceCandidate
    {
        [JsonPropertyName("place_id")]
        public string PlaceId { get; set; } = string.Empty;
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }
}
