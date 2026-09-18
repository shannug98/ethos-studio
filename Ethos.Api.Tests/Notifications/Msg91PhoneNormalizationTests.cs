using Ethos.Api.Application.Common;
using Ethos.Api.Application.Notifications;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ethos.Api.Tests.Notifications;

public class Msg91PhoneNormalizationTests
{
    private readonly Msg91WhatsAppService _service;

    public Msg91PhoneNormalizationTests()
    {
        var options = Options.Create(new Msg91Options());
        _service = new Msg91WhatsAppService(new HttpClient(), options, NullLogger<Msg91WhatsAppService>.Instance);
    }

    [Theory]
    [InlineData("9876543210", "919876543210")]
    [InlineData("+91 98765 43210", "919876543210")]
    [InlineData("+91-98765-43210", "919876543210")]
    [InlineData("09876543210", "919876543210")]
    [InlineData("919876543210", "919876543210")]
    [InlineData("6234567890", "916234567890")]
    [InlineData("7234567890", "917234567890")]
    [InlineData("8234567890", "918234567890")]
    public void TryNormalizePhoneNumber_AcceptsValidIndianMobileFormats(string input, string expected)
    {
        var isValid = _service.TryNormalizePhoneNumber(input, out var normalized, out var error);

        Assert.True(isValid, $"Expected valid phone for '{input}', but got error: {error}");
        Assert.Equal(expected, normalized);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("12345")]
    [InlineData("1234567890")] // Starts with 1 (not 6, 7, 8, 9)
    [InlineData("2234567890")] // Starts with 2
    [InlineData("98765abcd0")] // Contains letters
    [InlineData("phone+919876")] // Contains letters
    [InlineData("+91-98765-43210@home")] // Contains @ symbol
    [InlineData("123456789012345678")] // Exceeds 15 digits
    public void TryNormalizePhoneNumber_StrictlyRejectsMalformedOrAmbiguousNumbers(string? input)
    {
        var isValid = _service.TryNormalizePhoneNumber(input, out var normalized, out var error);

        Assert.False(isValid, $"Expected invalid phone for '{input}', but succeeded with: {normalized}");
        Assert.NotNull(error);
        Assert.Empty(normalized);
    }
}
