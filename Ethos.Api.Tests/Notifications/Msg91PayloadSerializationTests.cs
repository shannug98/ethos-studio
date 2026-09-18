using System.Text.Json;
using Ethos.Api.Application.Common;
using Ethos.Api.Application.Notifications;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ethos.Api.Tests.Notifications;

public class Msg91PayloadSerializationTests
{
    private readonly Msg91WhatsAppService _service;

    public Msg91PayloadSerializationTests()
    {
        var options = Options.Create(new Msg91Options
        {
            Enabled = true,
            AuthKey = "test_auth_key",
            IntegratedNumber = "919999988888",
            BookingConfirmedTemplateName = "ethos_booking_confirmed",
            BookingConfirmedNamespace = "df6202a5_c621_4e2b_90a3_9d5ef2e14f8a",
            TicketPdfTemplateName = "ethos_ticket_pdf",
            TicketPdfNamespace = null
        });

        _service = new Msg91WhatsAppService(new HttpClient(), options, NullLogger<Msg91WhatsAppService>.Instance);
    }

    [Fact]
    public void BuildBookingConfirmedJson_ContainsExactTemplateNamespaceAndParameters()
    {
        var data = new BookingConfirmedData(
            AttendeeName: "Rahul Sharma",
            WorkshopTitle: "Bollywood Masterclass",
            WorkshopDate: "25 October 2026",
            WorkshopTime: "6:00 PM – 8:00 PM",
            BookingId: "ETHOS-WKS-8F31A2C4");

        var json = _service.BuildBookingConfirmedJson(data, "919876543210");

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("919999988888", root.GetProperty("integrated_number").GetString());
        Assert.Equal("template", root.GetProperty("content_type").GetString());

        var payload = root.GetProperty("payload");
        Assert.Equal("whatsapp", payload.GetProperty("messaging_product").GetString());

        var template = payload.GetProperty("template");
        Assert.Equal("ethos_booking_confirmed", template.GetProperty("name").GetString());
        Assert.Equal("df6202a5_c621_4e2b_90a3_9d5ef2e14f8a", template.GetProperty("namespace").GetString());

        var toAndComponents = template.GetProperty("to_and_components")[0];
        Assert.Equal("919876543210", toAndComponents.GetProperty("to")[0].GetString());

        var comps = toAndComponents.GetProperty("components");
        Assert.Equal("Rahul Sharma", comps.GetProperty("body_1").GetProperty("value").GetString());
        Assert.Equal("Bollywood Masterclass", comps.GetProperty("body_2").GetProperty("value").GetString());
        Assert.Equal("25 October 2026", comps.GetProperty("body_3").GetProperty("value").GetString());
        Assert.Equal("6:00 PM – 8:00 PM", comps.GetProperty("body_4").GetProperty("value").GetString());
        Assert.Equal("ETHOS-WKS-8F31A2C4", comps.GetProperty("body_5").GetProperty("value").GetString());
    }

    [Fact]
    public void BuildTicketPdfJson_ExplicitlySerializesNullNamespaceAndDocumentHeader()
    {
        var data = new TicketPdfData(
            AttendeeName: "Rahul Sharma",
            WorkshopTitle: "Bollywood Masterclass",
            WorkshopDate: "25 October 2026",
            WorkshopTime: "6:00 PM – 8:00 PM",
            BookingId: "ETHOS-WKS-8F31A2C4",
            PdfHttpsUrl: "https://media.ethosdancestudio.com/tickets/ETHOS-TKT-001.pdf",
            FileName: "ETHOS-TKT-001.pdf");

        var json = _service.BuildTicketPdfJson(data, "919876543210");

        // Verify "namespace": null is present as raw text
        Assert.Contains("\"namespace\":null", json.Replace(" ", ""));

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var template = root.GetProperty("payload").GetProperty("template");

        Assert.Equal(JsonValueKind.Null, template.GetProperty("namespace").ValueKind);
        Assert.Equal("ethos_ticket_pdf", template.GetProperty("name").GetString());

        var toAndComponents = template.GetProperty("to_and_components")[0];
        var comps = toAndComponents.GetProperty("components");

        var header = comps.GetProperty("header_1");
        Assert.Equal("document", header.GetProperty("type").GetString());
        Assert.Equal("https://media.ethosdancestudio.com/tickets/ETHOS-TKT-001.pdf", header.GetProperty("value").GetString());
        Assert.Equal("ETHOS-TKT-001.pdf", header.GetProperty("filename").GetString());

        Assert.Equal("Rahul Sharma", comps.GetProperty("body_1").GetProperty("value").GetString());
        Assert.Equal("ETHOS-WKS-8F31A2C4", comps.GetProperty("body_5").GetProperty("value").GetString());
    }
}
