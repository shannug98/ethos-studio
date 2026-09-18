using System.Net;
using Ethos.Api.Application.Common;
using Ethos.Api.Application.Notifications;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ethos.Api.Tests.Notifications;

public class Msg91ServiceHttpTests
{
    private class FakeHttpMessageHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage> Handler { get; set; } =
            _ => new HttpResponseMessage(HttpStatusCode.OK);

        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(Handler(request));
        }
    }

    [Fact]
    public async Task SendBookingConfirmedAsync_WhenProviderReturns200_ReturnsAccepted()
    {
        var fakeHandler = new FakeHttpMessageHandler
        {
            Handler = req =>
            {
                Assert.Equal("test_secret_auth_key", req.Headers.GetValues("authkey").First());
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"status\":\"success\",\"request_id\":\"req_test_888\",\"message\":\"Message queued successfully\"}")
                };
            }
        };

        var options = Options.Create(new Msg91Options
        {
            Enabled = true,
            AuthKey = "test_secret_auth_key",
            IntegratedNumber = "919999988888"
        });

        var service = new Msg91WhatsAppService(new HttpClient(fakeHandler), options, NullLogger<Msg91WhatsAppService>.Instance);

        var data = new BookingConfirmedData(
            AttendeeName: "Rahul",
            WorkshopTitle: "Bollywood",
            WorkshopDate: "25 Oct 2026",
            WorkshopTime: "6 PM",
            BookingId: "BK-001");

        var result = await service.SendBookingConfirmedAsync(data, "9876543210");

        Assert.True(result.Success);
        Assert.Equal("req_test_888", result.ProviderRequestId);
        Assert.False(result.IsTimeout);
        Assert.False(result.IsTransientError);
        Assert.False(result.IsPermanentError);
    }

    [Fact]
    public async Task SendTicketPdfAsync_WhenUrlIsNotHttps_RejectsBeforeDispatch()
    {
        var fakeHandler = new FakeHttpMessageHandler();
        var options = Options.Create(new Msg91Options
        {
            Enabled = true,
            AuthKey = "test_auth_key",
            IntegratedNumber = "919999988888"
        });

        var service = new Msg91WhatsAppService(new HttpClient(fakeHandler), options, NullLogger<Msg91WhatsAppService>.Instance);

        var data = new TicketPdfData(
            AttendeeName: "Rahul",
            WorkshopTitle: "Bollywood",
            WorkshopDate: "25 Oct 2026",
            WorkshopTime: "6 PM",
            BookingId: "BK-001",
            PdfHttpsUrl: "http://localhost:5000/uploads/tickets/ticket.pdf", // INSECURE HTTP / LOCAL
            FileName: "ticket.pdf");

        var result = await service.SendTicketPdfAsync(data, "9876543210");

        Assert.False(result.Success);
        Assert.True(result.IsPermanentError);
        Assert.Contains("HTTPS URL", result.ErrorMessage);
        Assert.Null(fakeHandler.LastRequest); // Never made network request
    }

    [Fact]
    public async Task SendBookingConfirmedAsync_WhenProviderReturns500_MarksTransientError()
    {
        var fakeHandler = new FakeHttpMessageHandler
        {
            Handler = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("{\"status\":\"error\",\"message\":\"Internal server error at MSG91 gateway\"}")
            }
        };

        var options = Options.Create(new Msg91Options
        {
            Enabled = true,
            AuthKey = "test_auth_key",
            IntegratedNumber = "919999988888"
        });

        var service = new Msg91WhatsAppService(new HttpClient(fakeHandler), options, NullLogger<Msg91WhatsAppService>.Instance);

        var data = new BookingConfirmedData("Rahul", "Bollywood", "25 Oct", "6 PM", "BK-001");
        var result = await service.SendBookingConfirmedAsync(data, "9876543210");

        Assert.False(result.Success);
        Assert.True(result.IsTransientError);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task SendBookingConfirmedAsync_WhenDisabledOrUnconfigured_ReturnsSkipped()
    {
        var fakeHandler = new FakeHttpMessageHandler();
        var options = Options.Create(new Msg91Options
        {
            Enabled = false, // Disabled
            AuthKey = null,
            IntegratedNumber = null
        });

        var service = new Msg91WhatsAppService(new HttpClient(fakeHandler), options, NullLogger<Msg91WhatsAppService>.Instance);

        var data = new BookingConfirmedData("Rahul", "Bollywood", "25 Oct", "6 PM", "BK-001");
        var result = await service.SendBookingConfirmedAsync(data, "9876543210");

        Assert.False(result.Success);
        Assert.True(result.Skipped);
        Assert.Null(fakeHandler.LastRequest);
    }
}
