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
    public async Task<ActionResult<IReadOnlyList<VenueSuggestionDto>>> Autocomplete(
        [FromQuery] string query,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 3)
        {
            return Ok(Array.Empty<VenueSuggestionDto>());
        }

        var cleanQuery = query.Trim().ToLowerInvariant();
        var results = new List<VenueSuggestionDto>();

        // 1. Search previously saved Ethos studio venues from database
        var studioVenues = await _db.Workshops
            .AsNoTracking()
            .Where(w => w.Venue.ToLower().Contains(cleanQuery) || (w.City != null && w.City.ToLower().Contains(cleanQuery)))
            .Select(w => new VenueSuggestionDto
            {
                PlaceId = w.GooglePlaceId ?? $"studio_{w.Venue.Replace(" ", "_").ToLower()}",
                MainText = w.Venue,
                SecondaryText = $"{w.Area ?? ""}, {w.City ?? ""}".Trim(',', ' '),
                FullAddress = w.VenueAddress ?? w.Venue,
                City = w.City,
                Area = w.Area,
                Latitude = w.Latitude,
                Longitude = w.Longitude
            })
            .Distinct()
            .Take(5)
            .ToListAsync(cancellationToken);

        results.AddRange(studioVenues);

        // 2. Add standard known Ethos Studio venues if matching
        var knownEthosVenues = new List<VenueSuggestionDto>
        {
            new() { PlaceId = "ethos_main_a", MainText = "Ethos Main Studio A", SecondaryText = "Jubilee Hills, Hyderabad", FullAddress = "Plot 42, Road No 36, Jubilee Hills, Hyderabad", City = "Hyderabad", Area = "Jubilee Hills", Latitude = 17.4319, Longitude = 78.4073 },
            new() { PlaceId = "ethos_studio_b", MainText = "Ethos Studio B (Choreography Floor)", SecondaryText = "Banjara Hills, Hyderabad", FullAddress = "Road No 12, Banjara Hills, Hyderabad", City = "Hyderabad", Area = "Banjara Hills", Latitude = 17.4156, Longitude = 78.4350 },
            new() { PlaceId = "ethos_auditorium", MainText = "Ethos Grand Auditorium", SecondaryText = "Gachibowli, Hyderabad", FullAddress = "Financial District, Gachibowli, Hyderabad", City = "Hyderabad", Area = "Gachibowli", Latitude = 17.4401, Longitude = 78.3489 }
        };

        foreach (var v in knownEthosVenues)
        {
            if ((v.MainText.ToLower().Contains(cleanQuery) || v.SecondaryText.ToLower().Contains(cleanQuery)) &&
                !results.Any(r => r.MainText.Equals(v.MainText, StringComparison.OrdinalIgnoreCase)))
            {
                results.Add(v);
            }
        }

        // 3. If Google Maps API key is configured on server, query Google Places API
        var apiKey = _config["GoogleMaps:ApiKey"] ?? _config["GoogleMaps_ApiKey"];
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var url = $"https://maps.googleapis.com/maps/api/place/autocomplete/json?input={Uri.EscapeDataString(query)}&key={apiKey}&components=country:in";
                var response = await client.GetFromJsonAsync<GooglePlacesAutocompleteResponse>(url, cancellationToken);

                if (response?.Predictions != null)
                {
                    foreach (var pred in response.Predictions)
                    {
                        if (!results.Any(r => r.PlaceId == pred.PlaceId))
                        {
                            results.Add(new VenueSuggestionDto
                            {
                                PlaceId = pred.PlaceId,
                                MainText = pred.StructuredFormatting?.MainText ?? pred.Description,
                                SecondaryText = pred.StructuredFormatting?.SecondaryText ?? "",
                                FullAddress = pred.Description
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Google Places autocomplete request failed for query {Query}", query);
            }
        }

        return Ok(results.Take(10).ToList());
    }
}
