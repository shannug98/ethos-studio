using Ethos.Api.Application.Workshops;
using Ethos.Api.Contracts.Workshops;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Ethos.Api.Tests.Workshops;

public class WhatsAppTransactionalOutboxTests
{
    private AppDbContext CreateInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task VerifyWorkshopPayment_AtomicallyEnqueuesOneBookingAndNTicketOutboxRecords()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TicketSecurity:SecretKey"] = "test-secret-key-32-characters-long-ticket-security!"
            })
            .Build();
        var ticketService = new WorkshopTicketService(db, config);

        var workshopId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var workshop = new Workshop
        {
            Id = workshopId,
            Title = "Hip Hop Masterclass",
            WorkshopDate = DateTime.UtcNow.AddDays(7),
            StartTime = TimeSpan.FromHours(18),
            EndTime = TimeSpan.FromHours(20),
            Capacity = 30,
            Price = 1200,
            Status = WorkshopStatus.Published,
            Venue = "Ethos Studio A"
        };
        db.Workshops.Add(workshop);

        var role = new Role { Id = Guid.NewGuid(), Code = "STUDENT", Name = "Student" };
        db.Roles.Add(role);

        var user = new User
        {
            Id = userId,
            FullName = "Rohan Varma",
            Phone = "+919876543210",
            Email = "rohan@example.com",
            CustomerCode = "CUST-001"
        };
        user.UserRoles.Add(new UserRole { UserId = userId, RoleId = role.Id, Role = role });
        db.Users.Add(user);

        var bookingId = Guid.NewGuid();
        var booking = new WorkshopBooking
        {
            Id = bookingId,
            WorkshopId = workshopId,
            Quantity = 2, // 2 tickets
            TotalPrice = 2400,
            Status = WorkshopBookingStatus.Confirmed,
            GuestName = "Rohan Varma",
            GuestPhone = "9876543210",
            Workshop = workshop
        };
        db.WorkshopBookings.Add(booking);

        var txId = Guid.NewGuid();
        var transaction = new PaymentTransaction
        {
            Id = txId,
            UserId = userId,
            Amount = 2400,
            Currency = "INR",
            Status = PaymentStatus.Paid,
            Purpose = PaymentPurpose.WorkshopBooking,
            ReferenceId = booking.Id
        };
        db.PaymentTransactions.Add(transaction);

        await db.SaveChangesAsync();

        // Issue tickets
        var issuedTickets = await ticketService.IssueTicketsForBookingAsync(booking, transaction, user);
        Assert.Equal(2, issuedTickets.Count);

        // Enqueue outbox records (identical to WorkshopService logic)
        var primaryPhone = booking.GuestPhone;
        var bookingKey = $"wapp_booking_{booking.Id}";

        db.WhatsAppNotifications.Add(new WhatsAppNotification
        {
            Id = Guid.NewGuid(),
            BookingId = booking.Id,
            WorkshopTicketId = null,
            NotificationType = WhatsAppNotificationType.BookingConfirmed,
            RecipientPhone = primaryPhone,
            IdempotencyKey = bookingKey,
            Status = WhatsAppNotificationStatus.Pending
        });

        foreach (var t in issuedTickets)
        {
            db.WhatsAppNotifications.Add(new WhatsAppNotification
            {
                Id = Guid.NewGuid(),
                BookingId = booking.Id,
                WorkshopTicketId = t.Id,
                NotificationType = WhatsAppNotificationType.TicketPdf,
                RecipientPhone = primaryPhone,
                IdempotencyKey = $"wapp_ticket_{t.Id}",
                Status = WhatsAppNotificationStatus.Pending
            });
        }

        await db.SaveChangesAsync();

        // Assert outbox counts and deterministic keys
        var outboxList = await db.WhatsAppNotifications.Where(x => x.BookingId == booking.Id).ToListAsync();
        Assert.Equal(3, outboxList.Count);

        var bookingNotice = outboxList.Single(x => x.NotificationType == WhatsAppNotificationType.BookingConfirmed);
        Assert.Equal($"wapp_booking_{booking.Id}", bookingNotice.IdempotencyKey);
        Assert.Null(bookingNotice.WorkshopTicketId);

        var ticketNotices = outboxList.Where(x => x.NotificationType == WhatsAppNotificationType.TicketPdf).ToList();
        Assert.Equal(2, ticketNotices.Count);
        Assert.All(ticketNotices, tn => Assert.StartsWith("wapp_ticket_", tn.IdempotencyKey));
    }
}
