using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json.Serialization;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ethos.Api.Controllers.Admin;

public class VenueSuggestionDto
{
    public string PlaceId { get; set; } = string.Empty;
    public string MainText { get; set; } = string.Empty;
    public string SecondaryText { get; set; } = string.Empty;
    public string FullAddress { get; set; } = string.Empty;
    public string? City { get; set; }
    public string? Area { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string Source { get; set; } = "ethos"; // "ethos" | "google"
}

public class VenueSearchResponse
{
    public List<VenueSuggestionDto> EthosVenues { get; set; } = new();
    public List<VenueSuggestionDto> GooglePlaces { get; set; } = new();
    public bool GooglePlacesAvailable { get; set; }
    public string? Notice { get; set; }
}

public class GooglePlacesAutocompleteResponse
{
    [JsonPropertyName("predictions")]
    public List<GooglePlacePrediction>? Predictions { get; set; }
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
}

public class GooglePlacePrediction
{
    [JsonPropertyName("place_id")]
    public string PlaceId { get; set; } = string.Empty;
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
    [JsonPropertyName("structured_formatting")]
    public GooglePlaceStructuredFormatting? StructuredFormatting { get; set; }
}

public class GooglePlaceStructuredFormatting
{
    [JsonPropertyName("main_text")]
    public string MainText { get; set; } = string.Empty;
    [JsonPropertyName("secondary_text")]
    public string? SecondaryText { get; set; }
}

public class GooglePlaceDetailsResponse
{
    [JsonPropertyName("result")]
    public GooglePlaceDetailResult? Result { get; set; }
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
}

public class GooglePlaceDetailResult
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    [JsonPropertyName("formatted_address")]
    public string FormattedAddress { get; set; } = string.Empty;
    [JsonPropertyName("geometry")]
    public GooglePlaceGeometry? Geometry { get; set; }
    [JsonPropertyName("address_components")]
    public List<GoogleAddressComponent>? AddressComponents { get; set; }
}

public class GooglePlaceGeometry
{
    [JsonPropertyName("location")]
    public GoogleLocation? Location { get; set; }
}

public class GoogleLocation
{
    [JsonPropertyName("lat")]
    public double Lat { get; set; }
    [JsonPropertyName("lng")]
    public double Lng { get; set; }
}

public class GoogleAddressComponent
{
    [JsonPropertyName("long_name")]
    public string LongName { get; set; } = string.Empty;
    [JsonPropertyName("short_name")]
    public string ShortName { get; set; } = string.Empty;
    [JsonPropertyName("types")]
    public List<string> Types { get; set; } = new();
}

[ApiController]
[Route("api/admin/venues")]
[Authorize(Roles = "ADMIN")]
[Tags("Admin - Venue Search")]
public class AdminVenuesController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AdminVenuesController> _logger;

    public AdminVenuesController(
        IConfiguration config,
        AppDbContext db,
        IHttpClientFactory httpClientFactory,
        ILogger<AdminVenuesController> logger)
    {
        _config = config;
        _db = db;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [HttpGet("autocomplete")]
    public async Task<ActionResult<VenueSearchResponse>> Autocomplete(
        [FromQuery] string query,
        CancellationToken cancellationToken)
    {
        var response = new VenueSearchResponse();

        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
        {
            return Ok(response);
        }

        var cleanQuery = query.Trim().ToLowerInvariant();

        // 1. Search previously saved Ethos studio venues from database
        var studioVenues = await _db.Workshops
            .AsNoTracking()
            .Where(w => w.Venue.ToLower().Contains(cleanQuery) || (w.City != null && w.City.ToLower().Contains(cleanQuery)) || (w.Area != null && w.Area.ToLower().Contains(cleanQuery)))
            .Select(w => new VenueSuggestionDto
            {
                PlaceId = w.GooglePlaceId ?? $"studio_{w.Venue.Replace(" ", "_").ToLower()}",
                MainText = w.Venue,
                SecondaryText = $"{w.Area ?? ""}, {w.City ?? ""}".Trim(',', ' '),
                FullAddress = w.VenueAddress ?? w.Venue,
                City = w.City,
                Area = w.Area,
                Latitude = w.Latitude,
                Longitude = w.Longitude,
                Source = "ethos"
            })
            .Distinct()
            .Take(5)
            .ToListAsync(cancellationToken);

        response.EthosVenues.AddRange(studioVenues);

        // 2. Add standard known Ethos Studio venues if matching
        var knownEthosVenues = new List<VenueSuggestionDto>
        {
            new() { PlaceId = "ethos_main_a", MainText = "Ethos Main Studio A", SecondaryText = "Jubilee Hills, Hyderabad, Telangana", FullAddress = "Plot 42, Road No 36, Jubilee Hills, Hyderabad, Telangana", City = "Hyderabad", Area = "Jubilee Hills", Latitude = 17.4319, Longitude = 78.4073, Source = "ethos" },
            new() { PlaceId = "ethos_studio_b", MainText = "Ethos Studio B (Choreography Floor)", SecondaryText = "Banjara Hills, Hyderabad, Telangana", FullAddress = "Road No 12, Banjara Hills, Hyderabad, Telangana", City = "Hyderabad", Area = "Banjara Hills", Latitude = 17.4156, Longitude = 78.4350, Source = "ethos" },
            new() { PlaceId = "ethos_auditorium", MainText = "Ethos Grand Auditorium", SecondaryText = "Gachibowli, Hyderabad, Telangana", FullAddress = "Financial District, Gachibowli, Hyderabad, Telangana", City = "Hyderabad", Area = "Gachibowli", Latitude = 17.4401, Longitude = 78.3489, Source = "ethos" }
        };

        foreach (var v in knownEthosVenues)
        {
            if ((v.MainText.ToLower().Contains(cleanQuery) || v.SecondaryText.ToLower().Contains(cleanQuery)) &&
                !response.EthosVenues.Any(r => r.MainText.Equals(v.MainText, StringComparison.OrdinalIgnoreCase)))
            {
                response.EthosVenues.Add(v);
            }
        }

        // 3. Query Google Places API with India-wide scope (components=country:in)
        var apiKey = _config["GoogleMaps:ApiKey"] ?? _config["GoogleMaps_ApiKey"] ?? Environment.GetEnvironmentVariable("GOOGLE_MAPS_API_KEY");
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var url = $"https://maps.googleapis.com/maps/api/place/autocomplete/json?input={Uri.EscapeDataString(query)}&key={apiKey}&components=country:in";
                var googleRes = await client.GetFromJsonAsync<GooglePlacesAutocompleteResponse>(url, cancellationToken);

                if (googleRes?.Predictions != null && googleRes.Predictions.Count > 0)
                {
                    response.GooglePlacesAvailable = true;
                    foreach (var pred in googleRes.Predictions)
                    {
                        if (!response.GooglePlaces.Any(r => r.PlaceId == pred.PlaceId))
                        {
                            response.GooglePlaces.Add(new VenueSuggestionDto
                            {
                                PlaceId = pred.PlaceId,
                                MainText = pred.StructuredFormatting?.MainText ?? pred.Description,
                                SecondaryText = pred.StructuredFormatting?.SecondaryText ?? "",
                                FullAddress = pred.Description,
                                Source = "google"
                            });
                        }
                    }
                }
                else
                {
                    response.GooglePlacesAvailable = true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Google Places autocomplete request failed for query {Query}", query);
                response.GooglePlacesAvailable = false;
                response.Notice = "Google Places is unavailable. Search existing Ethos venues or enter the venue manually.";
            }
        }
        else
        {
            response.GooglePlacesAvailable = false;
            response.Notice = "Google Places is unavailable. Search existing Ethos venues or enter the venue manually.";
        }

        return Ok(response);
    }

    [HttpGet("details")]
    public async Task<ActionResult<VenueSuggestionDto>> GetPlaceDetails(
        [FromQuery] string placeId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(placeId))
            return BadRequest(new { message = "PlaceId is required." });

        // Check if Ethos known venue
        if (placeId.StartsWith("ethos_") || placeId.StartsWith("studio_"))
        {
            var ws = await _db.Workshops.AsNoTracking()
                .FirstOrDefaultAsync(w => w.GooglePlaceId == placeId, cancellationToken);
            if (ws != null)
            {
                return Ok(new VenueSuggestionDto
                {
                    PlaceId = placeId,
                    MainText = ws.Venue,
                    SecondaryText = $"{ws.Area ?? ""}, {ws.City ?? ""}".Trim(',', ' '),
                    FullAddress = ws.VenueAddress ?? ws.Venue,
                    City = ws.City,
                    Area = ws.Area,
                    Latitude = ws.Latitude,
                    Longitude = ws.Longitude,
                    Source = "ethos"
                });
            }
        }

        var apiKey = _config["GoogleMaps:ApiKey"] ?? _config["GoogleMaps_ApiKey"] ?? Environment.GetEnvironmentVariable("GOOGLE_MAPS_API_KEY");
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var url = $"https://maps.googleapis.com/maps/api/place/details/json?place_id={Uri.EscapeDataString(placeId)}&fields=name,formatted_address,geometry,address_components&key={apiKey}";
                var details = await client.GetFromJsonAsync<GooglePlaceDetailsResponse>(url, cancellationToken);

                if (details?.Result != null)
                {
                    string? city = null;
                    string? area = null;

                    if (details.Result.AddressComponents != null)
                    {
                        city = details.Result.AddressComponents.FirstOrDefault(c => c.Types.Contains("locality"))?.LongName
                               ?? details.Result.AddressComponents.FirstOrDefault(c => c.Types.Contains("administrative_area_level_2"))?.LongName;
                        area = details.Result.AddressComponents.FirstOrDefault(c => c.Types.Contains("sublocality") || c.Types.Contains("neighborhood"))?.LongName;
                    }

                    return Ok(new VenueSuggestionDto
                    {
                        PlaceId = placeId,
                        MainText = details.Result.Name,
                        FullAddress = details.Result.FormattedAddress,
                        SecondaryText = details.Result.FormattedAddress,
                        City = city,
                        Area = area,
                        Latitude = details.Result.Geometry?.Location?.Lat,
                        Longitude = details.Result.Geometry?.Location?.Lng,
                        Source = "google"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch place details for {PlaceId}", placeId);
            }
        }

        return NotFound(new { message = "Place details could not be retrieved. Please enter venue manually." });
    }
}
