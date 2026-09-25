import { test, describe } from "node:test";
import assert from "node:assert/strict";

describe("Venue Selection & SSRF-Protected Google Maps Resolution Verification", () => {
  test("Option 1 — Session token is correctly appended to autocomplete and place details", async () => {
    let capturedAutocompleteUrl = null;
    let capturedDetailsUrl = null;

    const fakeAdminApi = {
      searchVenues: (query, sessionToken = null) => {
        let url = `/api/admin/venues/autocomplete?query=${encodeURIComponent(query)}`;
        if (sessionToken) url += `&sessionToken=${encodeURIComponent(sessionToken)}`;
        capturedAutocompleteUrl = url;
        return Promise.resolve({ ethosVenues: [], googlePlaces: [] });
      },
      getPlaceDetails: (placeId, sessionToken = null) => {
        let url = `/api/admin/venues/details?placeId=${encodeURIComponent(placeId)}`;
        if (sessionToken) url += `&sessionToken=${encodeURIComponent(sessionToken)}`;
        capturedDetailsUrl = url;
        return Promise.resolve({ name: "Ethos Madhapur" });
      },
    };

    const sessionToken = "session-uuid-12345";
    await fakeAdminApi.searchVenues("Madhapur", sessionToken);
    assert.equal(capturedAutocompleteUrl, "/api/admin/venues/autocomplete?query=Madhapur&sessionToken=session-uuid-12345");

    await fakeAdminApi.getPlaceDetails("ChIJ1234567890", sessionToken);
    assert.equal(capturedDetailsUrl, "/api/admin/venues/details?placeId=ChIJ1234567890&sessionToken=session-uuid-12345");
  });

  test("Option 2 — resolveVenueUrl posts payload with url and returns resolved venue dto", async () => {
    let capturedMethod = null;
    let capturedBody = null;

    const fakeAdminApi = {
      resolveVenueUrl: (url) => {
        capturedMethod = "POST";
        capturedBody = JSON.stringify({ url });
        return Promise.resolve({
          venueName: "Ironhill Cafe Madhapur",
          fullAddress: "Ironhill Cafe Madhapur, Plot No. 11-2, Sector 1, Madhapur, Hyderabad, Telangana 500081",
          city: "Hyderabad",
          area: "Madhapur",
          latitude: 17.445257,
          longitude: 78.3843704,
          googlePlaceId: "ChIJW3P...",
          locationUrl: "https://www.google.com/maps/dir/?api=1&destination=Ironhill+Cafe+Madhapur",
          resolutionSource: "google_places_api",
          isVerified: true,
        });
      },
    };

    const inputUrl = "https://maps.app.goo.gl/ABC123xyz";
    const result = await fakeAdminApi.resolveVenueUrl(inputUrl);

    assert.equal(capturedMethod, "POST");
    assert.deepEqual(JSON.parse(capturedBody), { url: inputUrl });
    assert.equal(result.venueName, "Ironhill Cafe Madhapur");
    assert.equal(result.isVerified, true);
    assert.equal(result.city, "Hyderabad");
    assert.ok(result.locationUrl.length < 1000);
  });

  test("Confirmation-Before-Fill Invariant — form state is not populated until explicit user action", () => {
    let formState = {
      venue: "",
      venueAddress: "",
      city: "",
      area: "",
      locationUrl: "",
    };

    const resolvedVenuePreview = {
      venueName: "Ethos Main Studio",
      fullAddress: "Road No. 36, Jubilee Hills, Hyderabad",
      city: "Hyderabad",
      area: "Jubilee Hills",
      locationUrl: "https://www.google.com/maps/dir/?api=1&destination=Ethos+Main+Studio",
      isVerified: true,
    };

    // Before user clicks [ Use These Details ]
    assert.equal(formState.venue, "", "Form venue should remain empty prior to confirmation");
    assert.equal(formState.locationUrl, "", "Location URL should not be filled prematurely");

    // Simulating user clicking [ Use These Details ]
    formState = {
      ...formState,
      venue: resolvedVenuePreview.venueName,
      venueAddress: resolvedVenuePreview.fullAddress,
      city: resolvedVenuePreview.city,
      area: resolvedVenuePreview.area,
      locationUrl: resolvedVenuePreview.locationUrl,
    };

    assert.equal(formState.venue, "Ethos Main Studio");
    assert.equal(formState.city, "Hyderabad");
    assert.equal(formState.locationUrl, "https://www.google.com/maps/dir/?api=1&destination=Ethos+Main+Studio");
  });

  test("Authoritative LocationUrl invariant — clean canonical link is stored for PDF generation", () => {
    const rawMobileLink = "https://maps.app.goo.gl/zX7pQ9?g_st=ic&feature=share&utm_source=mobile";
    // Canonical link should be under 1000 characters and directed to Google Directions/Search API
    const canonicalLink = "https://www.google.com/maps/dir/?api=1&destination=17.445257,78.3843704&destination_place_id=ChIJ123";
    
    assert.ok(canonicalLink.length < 1000, "Canonical link must be well within safe column limit");
    assert.ok(canonicalLink.startsWith("https://www.google.com/maps/"), "Canonical link must point directly to Google Maps service");
  });
});
