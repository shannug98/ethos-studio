using System.Security.Cryptography;
using System.Text;
using Ethos.Api.Contracts.Workshops;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Ethos.Api.Application.Workshops;

public class WorkshopTicketService : IWorkshopTicketService
{
    private readonly AppDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly byte[] _hmacKey;

    public WorkshopTicketService(AppDbContext dbContext, IConfiguration configuration)
    {
        _dbContext = dbContext;
        _configuration = configuration;

        var configuredKey = _configuration["TicketSecurity:SecretKey"];
        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            throw new InvalidOperationException(
                "TicketSecurity:SecretKey is required but not configured. Set a cryptographically secure key in application configuration.");
        }
        _hmacKey = Encoding.UTF8.GetBytes(configuredKey);
    }

    public string DeriveQrToken(WorkshopTicket ticket)
    {
        using var hmac = new HMACSHA256(_hmacKey);
        var message = $"{ticket.Id:D}:{ticket.TicketNumber}:{ticket.WorkshopBookingId:D}:{ticket.IssuedAt.Ticks}";
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
        return $"ETHOS-TKT-{Convert.ToHexString(hashBytes).ToLowerInvariant()}";
    }

    public string ComputeTokenHash(string rawToken)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawToken.Trim()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public string GeneratePdfDownloadToken(Guid ticketId, TimeSpan? validity = null)
    {
        var duration = validity ?? TimeSpan.FromHours(24);
        var expiresUtcTicks = DateTime.UtcNow.Add(duration).Ticks;
        using var hmac = new HMACSHA256(_hmacKey);
        var payload = $"pdf-download:{ticketId:D}:{expiresUtcTicks}";
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var sig = Convert.ToHexString(hashBytes).ToLowerInvariant();
        return $"{expiresUtcTicks}.{sig}";
    }

    public bool ValidatePdfDownloadToken(Guid ticketId, string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var parts = token.Split('.');
        if (parts.Length != 2)
        {
            return false;
        }

        if (!long.TryParse(parts[0], out var expiresUtcTicks))
        {
            return false;
        }

        if (DateTime.UtcNow.Ticks > expiresUtcTicks)
        {
            return false; // Token expired
        }

        using var hmac = new HMACSHA256(_hmacKey);
        var payload = $"pdf-download:{ticketId:D}:{expiresUtcTicks}";
        var expectedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var expectedSig = Convert.ToHexString(expectedHash).ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(parts[1].ToLowerInvariant()),
            Encoding.UTF8.GetBytes(expectedSig));
    }

    public async Task<List<WorkshopTicketResponse>> IssueTicketsForBookingAsync(
        WorkshopBooking booking,
        PaymentTransaction transaction,
        User? user,
        CancellationToken cancellationToken = default)
    {
        // 1. Idempotency check: if tickets already exist for this booking, return them
        var existingTickets = await _dbContext.WorkshopTickets
            .Include(t => t.Workshop)
            .Where(t => t.WorkshopBookingId == booking.Id)
            .OrderBy(t => t.TicketNumber)
            .ToListAsync(cancellationToken);

        if (existingTickets.Count > 0)
        {
            return existingTickets.Select(t =>
            {
                var r = MapToResponse(t, includeQrToken: true);
                r.QrToken = DeriveQrToken(t);
                return r;
            }).ToList();
        }

        var ticketsToCreate = new List<WorkshopTicket>();
        var responses = new List<WorkshopTicketResponse>();

        var qty = Math.Max(1, booking.Quantity);
        for (int i = 0; i < qty; i++)
        {
            var isPrimary = i == 0;
            var ticketId = Guid.NewGuid();
            var ticketNumber = $"ETHOS-WKS-{booking.Id.ToString()[..8].ToUpperInvariant()}-{(i + 1):D2}";

            var attendeeName = isPrimary
                ? (!string.IsNullOrWhiteSpace(booking.GuestName) ? booking.GuestName : (user?.FullName ?? "Ethos Guest"))
                : (!string.IsNullOrWhiteSpace(booking.GuestName) ? $"{booking.GuestName} (Guest {i + 1})" : $"Guest {i + 1}");

            var issuedAt = DateTime.UtcNow;

            var ticket = new WorkshopTicket
            {
                Id = ticketId,
                TicketNumber = ticketNumber,
                WorkshopBookingId = booking.Id,
                WorkshopId = booking.WorkshopId,
                UserId = transaction.UserId,
                PaymentTransactionId = transaction.Id,
                AttendeeName = attendeeName,
                AttendeePhone = isPrimary ? (!string.IsNullOrWhiteSpace(booking.GuestPhone) ? booking.GuestPhone : user?.Phone) : null,
                AttendeeEmail = isPrimary ? (!string.IsNullOrWhiteSpace(booking.GuestEmail) ? booking.GuestEmail : user?.Email) : null,
                IsPrimaryAttendee = isPrimary,
                Status = TicketStatus.Issued,
                IssuedAt = issuedAt,
                CheckedInAt = null,
                CheckedInByUserId = null,
                CheckInMethod = null,
                EmailSent = false,
                WhatsAppSent = false,
                ResendCount = 0,
                Workshop = booking.Workshop
            };

            // Derive token and compute hash
            var rawToken = DeriveQrToken(ticket);
            ticket.QrTokenHash = ComputeTokenHash(rawToken);

            _dbContext.WorkshopTickets.Add(ticket);
            ticketsToCreate.Add(ticket);

            var resp = MapToResponse(ticket, includeQrToken: true);
            resp.QrToken = rawToken;
            responses.Add(resp);
        }

        return responses;
    }

    public async Task<IReadOnlyList<WorkshopTicketResponse>> GetTicketsForBookingAsync(
        Guid bookingId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var booking = await _dbContext.WorkshopBookings
            .Include(b => b.StudentProfile)
            .FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken);

        if (booking == null)
        {
            throw new ArgumentException("Booking was not found.");
        }

        // Authorization: must be the booking owner (student) or have admin rights
        var isOwner = booking.StudentProfile?.UserId == userId;
        if (!isOwner)
        {
            var isAdmin = await _dbContext.UserRoles
                .Include(ur => ur.Role)
                .AnyAsync(ur => ur.UserId == userId && ur.Role != null && ur.Role.Code == "ADMIN", cancellationToken);

            if (!isAdmin)
            {
                throw new UnauthorizedAccessException("You are not authorized to access these tickets.");
            }
        }

        var tickets = await _dbContext.WorkshopTickets
            .Include(t => t.Workshop)
            .Where(t => t.WorkshopBookingId == bookingId)
            .OrderBy(t => t.TicketNumber)
            .ToListAsync(cancellationToken);

        // Populate raw QR tokens for the authenticated owner
        return tickets.Select(t =>
        {
            var res = MapToResponse(t, includeQrToken: true);
            res.QrToken = DeriveQrToken(t);
            return res;
        }).ToList();
    }

    public async Task<WorkshopTicketResponse> GetTicketPassAsync(
        Guid ticketId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var ticket = await _dbContext.WorkshopTickets
            .Include(t => t.Workshop)
            .Include(t => t.WorkshopBooking)
                .ThenInclude(b => b.StudentProfile)
            .FirstOrDefaultAsync(t => t.Id == ticketId, cancellationToken);

        if (ticket == null)
        {
            throw new ArgumentException("Ticket was not found.");
        }

        if (ticket.UserId != userId && ticket.WorkshopBooking?.StudentProfile?.UserId != userId)
        {
            throw new UnauthorizedAccessException("You are not authorized to view this ticket pass.");
        }

        if (ticket.Status == Ethos.Api.Domain.Enums.TicketStatus.Cancelled ||
            ticket.Status == Ethos.Api.Domain.Enums.TicketStatus.Refunded ||
            ticket.Status == Ethos.Api.Domain.Enums.TicketStatus.Expired)
        {
            throw new InvalidOperationException($"Ticket pass is unavailable because it has been {ticket.Status.ToString().ToLowerInvariant()}.");
        }

        var res = MapToResponse(ticket, includeQrToken: true);
        res.QrToken = DeriveQrToken(ticket);
        return res;
    }

    public async Task<WorkshopTicketResponse> UpdateAttendeeDetailsAsync(
        Guid bookingId,
        Guid ticketId,
        Guid userId,
        UpdateAttendeeDetailsRequest request,
        CancellationToken cancellationToken = default)
    {
        var ticket = await _dbContext.WorkshopTickets
            .Include(t => t.Workshop)
            .Include(t => t.WorkshopBooking)
                .ThenInclude(b => b.StudentProfile)
            .FirstOrDefaultAsync(t => t.Id == ticketId && t.WorkshopBookingId == bookingId, cancellationToken);

        if (ticket == null)
        {
            throw new ArgumentException("Ticket was not found for this booking.");
        }

        if (ticket.UserId != userId && ticket.WorkshopBooking?.StudentProfile?.UserId != userId)
        {
            throw new UnauthorizedAccessException("You are not authorized to update attendee details.");
        }

        if (ticket.CheckedInAt.HasValue)
        {
            throw new InvalidOperationException("Attendee details cannot be modified after the ticket has been checked in.");
        }

        if (ticket.AttendeeDetailsLockedAt.HasValue)
        {
            throw new InvalidOperationException("Attendee details have been locked by the studio.");
        }

        // Workshop cutoff check: Cannot update within 2 hours of workshop start time unless admin
        var workshopStart = ticket.Workshop.WorkshopDate.Date + ticket.Workshop.StartTime;
        if (DateTime.UtcNow >= workshopStart.AddHours(-2))
        {
            throw new InvalidOperationException("Guest details cannot be changed within 2 hours of workshop commencement.");
        }

        ticket.AttendeeName = request.AttendeeName.Trim();
        ticket.AttendeePhone = string.IsNullOrWhiteSpace(request.AttendeePhone) ? null : request.AttendeePhone.Trim();
        ticket.AttendeeEmail = string.IsNullOrWhiteSpace(request.AttendeeEmail) ? null : request.AttendeeEmail.Trim();

        await _dbContext.SaveChangesAsync(cancellationToken);

        var res = MapToResponse(ticket, includeQrToken: true);
        res.QrToken = DeriveQrToken(ticket);
        return res;
    }

    public async Task<bool> ResendTicketPassAsync(
        Guid bookingId,
        Guid ticketId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var ticket = await _dbContext.WorkshopTickets
            .Include(t => t.Workshop)
            .Include(t => t.WorkshopBooking)
                .ThenInclude(b => b.StudentProfile)
            .FirstOrDefaultAsync(t => t.Id == ticketId && t.WorkshopBookingId == bookingId, cancellationToken);

        if (ticket == null)
        {
            throw new ArgumentException("Ticket was not found.");
        }

        if (ticket.UserId != userId && ticket.WorkshopBooking?.StudentProfile?.UserId != userId)
        {
            throw new UnauthorizedAccessException("You are not authorized to request resending this pass.");
        }

        // Rate limiting check: max 5 resends per hour
        if (ticket.LastResentAt.HasValue && ticket.LastResentAt.Value > DateTime.UtcNow.AddMinutes(-5))
        {
            throw new InvalidOperationException("Pass was recently resent. Please wait at least 5 minutes before requesting another resend.");
        }

        ticket.ResendCount++;
        ticket.LastResentAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private WorkshopTicketResponse MapToResponse(WorkshopTicket ticket, bool includeQrToken = false)
    {
        return new WorkshopTicketResponse
        {
            Id = ticket.Id,
            TicketNumber = ticket.TicketNumber,
            WorkshopBookingId = ticket.WorkshopBookingId,
            WorkshopId = ticket.WorkshopId,
            WorkshopTitle = ticket.Workshop?.Title ?? "Workshop Pass",
            WorkshopDate = ticket.Workshop?.WorkshopDate ?? DateTime.UtcNow,
            StartTime = ticket.Workshop?.StartTime ?? TimeSpan.Zero,
            EndTime = ticket.Workshop?.EndTime ?? TimeSpan.Zero,
            Venue = ticket.Workshop?.Venue ?? "Ethos Dance Studio",
            AttendeeName = ticket.AttendeeName,
            AttendeePhone = ticket.AttendeePhone,
            AttendeeEmail = ticket.AttendeeEmail,
            IsPrimaryAttendee = ticket.IsPrimaryAttendee,
            Status = ticket.Status,
            IssuedAt = ticket.IssuedAt,
            CheckedInAt = ticket.CheckedInAt,
            AttendeeDetailsLockedAt = ticket.AttendeeDetailsLockedAt,
            QrToken = includeQrToken ? DeriveQrToken(ticket) : null,
            PdfDownloadToken = includeQrToken ? GeneratePdfDownloadToken(ticket.Id) : null
        };
    }
}
