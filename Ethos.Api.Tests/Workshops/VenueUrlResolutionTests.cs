using System.Net;
using System.Security;
using Ethos.Api.Application.Admin;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ethos.Api.Tests.Workshops;

public class VenueUrlResolutionTests
{
    private class DummyHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new HttpClient();
    }

    private readonly VenueUrlResolverService _resolver;

    public VenueUrlResolutionTests()
    {
        var config = new ConfigurationBuilder().Build();
        _resolver = new VenueUrlResolverService(config, new DummyHttpClientFactory(), NullLogger<VenueUrlResolverService>.Instance);
    }

    [Theory]
    [InlineData("http://localhost")]
    [InlineData("http://127.0.0.1")]
    [InlineData("https://malicious-site.com")]
    [InlineData("https://google.com.attacker.com/maps")]
    [InlineData("https://maps.google.com.evil.org")]
    [InlineData("https://169.254.169.254/latest/meta-data")]
    public void ValidateSsrfSafeHost_RejectsNonGoogleHosts(string url)
    {
        var uri = new Uri(url);
        var isValid = _resolver.ValidateSsrfSafeHost(uri);
        Assert.False(isValid, $"Host '{uri.Host}' should have been rejected by SSRF host whitelist.");
    }

    [Theory]
    [InlineData("https://maps.google.com/maps/place/test")]
    [InlineData("https://www.google.com/maps/dir//test")]
    [InlineData("https://google.com/maps")]
    [InlineData("https://maps.google.co.in/maps")]
    [InlineData("https://www.google.co.in/maps")]
    [InlineData("https://maps.app.goo.gl/abc1234")]
    [InlineData("https://goo.gl/maps/xyz9876")]
    public void ValidateSsrfSafeHost_AcceptsLegitimateGoogleHosts(string url)
    {
        var uri = new Uri(url);
        var isValid = _resolver.ValidateSsrfSafeHost(uri);
        Assert.True(isValid, $"Host '{uri.Host}' should be accepted.");
    }

    [Theory]
    [InlineData("127.0.0.1", true)]
    [InlineData("127.0.0.254", true)]
    [InlineData("10.0.0.1", true)]
    [InlineData("10.255.255.255", true)]
    [InlineData("172.16.0.1", true)]
    [InlineData("172.31.255.255", true)]
    [InlineData("192.168.0.1", true)]
    [InlineData("192.168.255.254", true)]
    [InlineData("169.254.169.254", true)]
    [InlineData("169.254.1.1", true)]
    [InlineData("0.0.0.0", true)]
    [InlineData("8.8.8.8", false)]
    [InlineData("1.1.1.1", false)]
    [InlineData("142.250.190.46", false)] // Google IP
    public void IsPrivateOrReservedIp_IdentifiesPrivateAndMetadataIps(string ipStr, bool shouldBeBlocked)
    {
        var ip = IPAddress.Parse(ipStr);
        var isBlocked = _resolver.IsPrivateOrReservedIp(ip);
        Assert.Equal(shouldBeBlocked, isBlocked);
    }

    [Fact]
    public async Task ResolveGoogleMapsUrlAsync_RejectsOversizedUrl()
    {
        var oversizedUrl = "https://maps.google.com/" + new string('a', 2100);
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _resolver.ResolveGoogleMapsUrlAsync(oversizedUrl));
        Assert.Contains("exceeds the maximum limit", ex.Message);
    }

    [Fact]
    public async Task ResolveGoogleMapsUrlAsync_RejectsForbiddenScheme()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _resolver.ResolveGoogleMapsUrlAsync("javascript:alert(1)"));
        Assert.Contains("Only valid HTTP or HTTPS URLs", ex.Message);
    }

    [Fact]
    public async Task ResolveGoogleMapsUrlAsync_RejectsLocalhost()
    {
        await Assert.ThrowsAsync<SecurityException>(() =>
            _resolver.ResolveGoogleMapsUrlAsync("http://localhost/maps"));
    }

    [Fact]
    public async Task ResolveGoogleMapsUrlAsync_RejectsMetadataIp()
    {
        await Assert.ThrowsAsync<SecurityException>(() =>
            _resolver.ResolveGoogleMapsUrlAsync("http://169.254.169.254/latest/meta-data"));
    }

    [Fact]
    public async Task ResolveGoogleMapsUrlAsync_FallbackParser_ExtractsNameAddressAndCoords()
    {
        var sampleUrl = "https://www.google.com/maps/place/Ironhill+Cafe+Madhapur/@17.445257,78.3843704,17z/data=!4m6!3m5!1s0x3bcb9158f201b205:0x11ab22cd33ef44gh";
        var result = await _resolver.ResolveGoogleMapsUrlAsync(sampleUrl);

        Assert.NotNull(result);
        Assert.Equal("Ironhill Cafe Madhapur", result.VenueName);
        Assert.Equal(17.445257, result.Latitude);
        Assert.Equal(78.3843704, result.Longitude);
        Assert.False(result.IsVerified);
        Assert.Equal("url_metadata_fallback", result.ResolutionSource);
        Assert.NotNull(result.LocationUrl);
        Assert.Contains("17.445257,78.3843704", result.LocationUrl);
        Assert.True(result.LocationUrl.Length < 1000);
    }

    [Fact]
    public async Task ResolveGoogleMapsUrlAsync_DirectionsUrl_ExtractsPlaceDetails()
    {
        var sampleUrl = "https://www.google.com/maps/dir//Ironhill+Cafe+Madhapur,+Plot+No.+11-2,+Sector+1,+Madhapur,+Hyderabad,+Telangana+500081/@17.445257,78.3843704,17z";
        var result = await _resolver.ResolveGoogleMapsUrlAsync(sampleUrl);

        Assert.NotNull(result);
        Assert.Equal("Ironhill Cafe Madhapur", result.VenueName);
        Assert.Equal("Hyderabad", result.City);
        Assert.False(string.IsNullOrWhiteSpace(result.Area));
        Assert.Equal(17.445257, result.Latitude);
        Assert.Equal(78.3843704, result.Longitude);
        Assert.False(result.IsVerified);
        Assert.Equal("url_metadata_fallback", result.ResolutionSource);
        Assert.True(result.LocationUrl?.Length < 1000);
    }

    [Fact]
    public async Task ResolveGoogleMapsUrlAsync_CoordinatesOnlyUrl_HandlesGracefully()
    {
        var sampleUrl = "https://www.google.com/maps/@17.445257,78.3843704,17z";
        var result = await _resolver.ResolveGoogleMapsUrlAsync(sampleUrl);

        Assert.NotNull(result);
        Assert.Equal(17.445257, result.Latitude);
        Assert.Equal(78.3843704, result.Longitude);
        Assert.Contains("17.4453", result.VenueName);
        Assert.False(result.IsVerified);
        Assert.Equal("url_metadata_fallback", result.ResolutionSource);
    }
}
