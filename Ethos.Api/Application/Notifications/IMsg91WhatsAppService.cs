namespace Ethos.Api.Application.Notifications;

public interface IMsg91WhatsAppService
{
    Task<Msg91DispatchResult> SendBookingConfirmedAsync(
        BookingConfirmedData data,
        string recipientPhone,
        CancellationToken cancellationToken = default);

    Task<Msg91DispatchResult> SendTicketPdfAsync(
        TicketPdfData data,
        string recipientPhone,
        CancellationToken cancellationToken = default);

    bool TryNormalizePhoneNumber(
        string? rawPhone,
        out string normalizedPhone,
        out string? failureReason,
        string defaultCountryCode = "91");

    string BuildBookingConfirmedJson(
        BookingConfirmedData data,
        string recipientPhone);

    string BuildTicketPdfJson(
        TicketPdfData data,
        string recipientPhone);
}
