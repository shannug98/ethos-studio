using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ethos.Api.Application.Admin;
using Ethos.Api.Application.Common;
using Ethos.Api.Application.Workshops;
using Ethos.Api.Contracts.Admin;
using Ethos.Api.Contracts.Workshops;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Domain.Exceptions;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ethos.Api.Tests.Workshops;

public class WorkshopDatesAndSessionsTests
{
    private class DummyAuditService : IAdminAuditService
    {
        public void AddAuditLog(
            Guid adminUserId, string actionType, string entityType, Guid entityId,
            string? reason, string category = "OPERATIONS", Guid? adminDeviceId = null,
            Guid? adminSessionId = null, string? traceId = null, string? ipAddress = null,
            string? userAgent = null, string? metadataJson = null)
        {
        }

        public Task LogActionAsync(
            Guid adminUserId, string actionType, string category, string entityType,
            Guid entityId, bool success = true, string? outcomeCode = null, string? reason = null,
            Guid? adminDeviceId = null, Guid? adminSessionId = null, string? traceId = null,
            string? requestId = null, string? ipAddress = null, string? userAgent = null,
            object? metadata = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task LogSecurityEventAsync(
            string eventType, string severity, string? ipAddress, string? userAgent,
            Guid? userId = null, Guid? adminDeviceId = null, Guid? adminSessionId = null,
            string? traceId = null, string? maskedPhone = null, object? details = null,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<PagedResult<AdminAuditLogResponse>> GetAuditLogsAsync(
            int page, int pageSize, string? category = null, string? actionType = null,
            string? entityType = null, Guid? entityId = null, Guid? adminUserId = null,
            string? traceId = null, DateTime? startDate = null, DateTime? endDate = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new PagedResult<AdminAuditLogResponse> { Items = new List<AdminAuditLogResponse>(), Page = 1, PageSize = 10, TotalCount = 0 });

        public Task<AdminAuditLogResponse?> GetAuditLogByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<AdminAuditLogResponse?>(null);

        public Task<PagedResult<AdminSecurityEventResponse>> GetSecurityEventsAsync(
            int page, int pageSize, string? eventType = null, string? severity = null,
            Guid? userId = null, string? traceId = null, DateTime? startDate = null,
            DateTime? endDate = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new PagedResult<AdminSecurityEventResponse> { Items = new List<AdminSecurityEventResponse>(), Page = 1, PageSize = 10, TotalCount = 0 });

        public Task<AdminSecurityEventResponse?> GetSecurityEventByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<AdminSecurityEventResponse?>(null);
    }

    private class DummyPricingService : IWorkshopPricingService
    {
        public Task<WorkshopPricingResponse> CalculatePricingAsync(Workshop workshop, Guid? userId = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new WorkshopPricingResponse
            {
                WorkshopId = workshop.Id,
                StartingPrice = workshop.Price,
                CurrentPrice = workshop.Price
            });
        }

        public Task<WorkshopPriceQuoteResponse> CalculateQuoteAsync(Guid workshopId, int quantity, Guid? userId = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new WorkshopPriceQuoteResponse
            {
                WorkshopId = workshopId,
                RequestedQuantity = quantity,
                TotalAmount = 500 * quantity
            });
        }

        public Task EnsureDefaultTiersAsync(Workshop workshop, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public decimal CalculatePublicPrice(decimal startingPrice, int bookedSeats) => startingPrice;
        public decimal CalculateStudentPrice(decimal startingPrice) => startingPrice;

        public Task<WorkshopPriceQuoteResponse> CalculateTicketTypeQuoteAsync(WorkshopPassType ticketType, int quantity, int currentTicketsSold, Guid? userId = null, CancellationToken cancellationToken = default)
        {
            var unitPrice = ticketType.Price > 0 ? ticketType.Price : 500m;
            return Task.FromResult(new WorkshopPriceQuoteResponse
            {
                WorkshopId = ticketType.WorkshopId,
                RequestedQuantity = quantity,
                TotalAmount = unitPrice * quantity,
                Breakdown = new List<WorkshopPriceQuoteItem>
                {
                    new() { TierNumber = 1, TierName = "Standard", Quantity = quantity, UnitPrice = unitPrice, Subtotal = unitPrice * quantity }
                }
            });
        }

        public void ValidateTicketTypePricingTiers(int totalQuantity, List<AdminWorkshopPricingTierItem>? tiers) { }
    }

    private DbContextOptions<AppDbContext> CreateOptions(string dbName)
    {
        return new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
    }

    private AdminWorkshopService CreateService(AppDbContext db)
    {
        return new AdminWorkshopService(db, new DummyAuditService(), new DummyPricingService());
    }

    private async Task<(Guid adminId, Guid trainer1Id, Guid trainer2Id)> SetupTrainersAsync(AppDbContext db)
    {
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Code == "ADMIN");
        if (role == null)
        {
            role = new Role { Id = Guid.NewGuid(), Code = "ADMIN", Name = "Admin" };
            db.Roles.Add(role);
        }

        var adminId = Guid.NewGuid();
        var adminUser = new User
        {
            Id = adminId,
            Phone = "+919999999999",
            FullName = "Admin",
            CustomerCode = "ADM01",
            CreatedAt = DateTime.UtcNow
        };
        adminUser.UserRoles.Add(new UserRole { UserId = adminId, RoleId = role.Id, Role = role });
        db.Users.Add(adminUser);

        var trainer1 = new TrainerProfile
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            TrainerCode = "TR01",
            FullName = "Trainer One",
            Status = TrainerStatus.Active,
            PrimaryDanceStyle = "Hip Hop",
            CreatedAt = DateTime.UtcNow
        };
        var trainer2 = new TrainerProfile
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            TrainerCode = "TR02",
            FullName = "Trainer Two",
            Status = TrainerStatus.Active,
            PrimaryDanceStyle = "Contemporary",
            CreatedAt = DateTime.UtcNow
        };

        db.TrainerProfiles.AddRange(trainer1, trainer2);
        await db.SaveChangesAsync();

        return (adminId, trainer1.Id, trainer2.Id);
    }

    [Fact]
    public async Task SingleDate_Workshop_Synchronizes_DatesTimesAndCapacity()
    {
        var options = CreateOptions(nameof(SingleDate_Workshop_Synchronizes_DatesTimesAndCapacity));
        using var db = new AppDbContext(options);
        var (adminId, trainer1Id, _) = await SetupTrainersAsync(db);
        var service = CreateService(db);

        var date = new DateTime(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc);
        var createReq = new AdminCreateWorkshopRequest
        {
            Title = "Single Date Intensive",
            DanceStyle = "Hip Hop",
            Level = "All Levels",
            Venue = "Main Hall",
            City = "Hyderabad",
            Capacity = 20,
            Price = 600,
            WorkshopDate = date,
            StartTime = new TimeSpan(14, 0, 0),
            EndTime = new TimeSpan(17, 0, 0),
            TrainerProfileId = trainer1Id,
            Sessions = new List<AdminWorkshopSessionItem>
            {
                new()
                {
                    SessionDate = date,
                    StartTime = new TimeSpan(10, 0, 0),
                    EndTime = new TimeSpan(11, 30, 0),
                    Capacity = 30,
                    Title = "Morning Grooves",
                    TrainerProfileId = trainer1Id
                },
                new()
                {
                    SessionDate = date,
                    StartTime = new TimeSpan(12, 0, 0),
                    EndTime = new TimeSpan(13, 30, 0),
                    Capacity = 35,
                    Title = "Afternoon Choreography",
                    TrainerProfileId = trainer1Id
                }
            }
        };

        var created = await service.CreateWorkshopAsync(adminId, createReq, CancellationToken.None);
        var ws = await db.Workshops.Include(w => w.Sessions).FirstOrDefaultAsync(w => w.Id == created.Id);

        Assert.NotNull(ws);
        Assert.Equal(2, ws!.Sessions.Count);
        Assert.Equal(date.Date, ws.WorkshopDate.Date);
        Assert.Equal(new TimeSpan(10, 0, 0), ws.StartTime);
        Assert.Equal(new TimeSpan(13, 30, 0), ws.EndTime);
        Assert.Equal(35, ws.Capacity); // Synced to max session capacity

        // Verify UTC range in Asia/Kolkata
        var tzId = OperatingSystem.IsWindows() ? "India Standard Time" : "Asia/Kolkata";
        var tz = TimeZoneInfo.FindSystemTimeZoneById(tzId);
        var expectedStartUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(date.Date, DateTimeKind.Unspecified) + new TimeSpan(10, 0, 0), tz);
        var expectedEndUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(date.Date, DateTimeKind.Unspecified) + new TimeSpan(13, 30, 0), tz);
        Assert.Equal(expectedStartUtc, ws.StartUtc);
        Assert.Equal(expectedEndUtc, ws.EndUtc);
    }

    [Fact]
    public async Task MultiDate_Consecutive_Workshop_Synchronizes_EarliestAndLatestRange()
    {
        var options = CreateOptions(nameof(MultiDate_Consecutive_Workshop_Synchronizes_EarliestAndLatestRange));
        using var db = new AppDbContext(options);
        var (adminId, trainer1Id, trainer2Id) = await SetupTrainersAsync(db);
        var service = CreateService(db);

        var day1 = new DateTime(2026, 11, 14, 0, 0, 0, DateTimeKind.Utc);
        var day2 = new DateTime(2026, 11, 15, 0, 0, 0, DateTimeKind.Utc);

        var createReq = new AdminCreateWorkshopRequest
        {
            Title = "Weekend Bootcamp",
            DanceStyle = "Commercial",
            Level = "Intermediate",
            Venue = "Ethos Studio A",
            City = "Bengaluru",
            Capacity = 25,
            Price = 1200,
            WorkshopDate = day1,
            StartTime = new TimeSpan(12, 0, 0),
            EndTime = new TimeSpan(18, 0, 0),
            TrainerProfileId = trainer1Id,
            TrainerProfileIds = new List<Guid> { trainer1Id, trainer2Id },
            Sessions = new List<AdminWorkshopSessionItem>
            {
                new()
                {
                    SessionDate = day1,
                    StartTime = new TimeSpan(11, 0, 0),
                    EndTime = new TimeSpan(13, 0, 0),
                    Capacity = 25,
                    Title = "Day 1 Foundations",
                    TrainerProfileId = trainer1Id
                },
                new()
                {
                    SessionDate = day2,
                    StartTime = new TimeSpan(15, 0, 0),
                    EndTime = new TimeSpan(18, 0, 0),
                    Capacity = 40,
                    Title = "Day 2 Performance",
                    TrainerProfileId = trainer2Id
                }
            }
        };

        var created = await service.CreateWorkshopAsync(adminId, createReq, CancellationToken.None);
        var ws = await db.Workshops.Include(w => w.Sessions).FirstOrDefaultAsync(w => w.Id == created.Id);

        Assert.NotNull(ws);
        Assert.Equal(day1.Date, ws!.WorkshopDate.Date);
        Assert.Equal(new TimeSpan(11, 0, 0), ws.StartTime);
        Assert.Equal(new TimeSpan(18, 0, 0), ws.EndTime);
        Assert.Equal(40, ws.Capacity);

        var tzId = OperatingSystem.IsWindows() ? "India Standard Time" : "Asia/Kolkata";
        var tz = TimeZoneInfo.FindSystemTimeZoneById(tzId);
        var expectedStartUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(day1.Date, DateTimeKind.Unspecified) + new TimeSpan(11, 0, 0), tz);
        var expectedEndUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(day2.Date, DateTimeKind.Unspecified) + new TimeSpan(18, 0, 0), tz);
        Assert.Equal(expectedStartUtc, ws.StartUtc);
        Assert.Equal(expectedEndUtc, ws.EndUtc);
    }

    [Fact]
    public async Task MultiDate_NonConsecutive_Workshop_Synchronizes_FullRange()
    {
        var options = CreateOptions(nameof(MultiDate_NonConsecutive_Workshop_Synchronizes_FullRange));
        using var db = new AppDbContext(options);
        var (adminId, trainer1Id, _) = await SetupTrainersAsync(db);
        var service = CreateService(db);

        var week1 = new DateTime(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc);
        var week2 = new DateTime(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc);
        var week3 = new DateTime(2026, 10, 17, 0, 0, 0, DateTimeKind.Utc);

        var createReq = new AdminCreateWorkshopRequest
        {
            Title = "3-Week Saturday Series",
            DanceStyle = "Popping",
            Level = "Open Level",
            Venue = "Ethos Main",
            City = "Hyderabad",
            Capacity = 30,
            Price = 1500,
            WorkshopDate = week1,
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(12, 0, 0),
            TrainerProfileId = trainer1Id,
            Sessions = new List<AdminWorkshopSessionItem>
            {
                new()
                {
                    SessionDate = week1,
                    StartTime = new TimeSpan(10, 0, 0),
                    EndTime = new TimeSpan(12, 0, 0),
                    Capacity = 30,
                    Title = "Week 1: Angles",
                    TrainerProfileId = trainer1Id
                },
                new()
                {
                    SessionDate = week2,
                    StartTime = new TimeSpan(10, 0, 0),
                    EndTime = new TimeSpan(12, 0, 0),
                    Capacity = 30,
                    Title = "Week 2: Waving",
                    TrainerProfileId = trainer1Id
                },
                new()
                {
                    SessionDate = week3,
                    StartTime = new TimeSpan(10, 0, 0),
                    EndTime = new TimeSpan(13, 0, 0),
                    Capacity = 30,
                    Title = "Week 3: Battles & Freestyle",
                    TrainerProfileId = trainer1Id
                }
            }
        };

        var created = await service.CreateWorkshopAsync(adminId, createReq, CancellationToken.None);
        var ws = await db.Workshops.Include(w => w.Sessions).FirstOrDefaultAsync(w => w.Id == created.Id);

        Assert.NotNull(ws);
        Assert.Equal(3, ws!.Sessions.Count);
        Assert.Equal(week1.Date, ws.WorkshopDate.Date);
        Assert.Equal(new TimeSpan(10, 0, 0), ws.StartTime);
        Assert.Equal(new TimeSpan(13, 0, 0), ws.EndTime);

        var tzId = OperatingSystem.IsWindows() ? "India Standard Time" : "Asia/Kolkata";
        var tz = TimeZoneInfo.FindSystemTimeZoneById(tzId);
        var expectedStartUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(week1.Date, DateTimeKind.Unspecified) + new TimeSpan(10, 0, 0), tz);
        var expectedEndUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(week3.Date, DateTimeKind.Unspecified) + new TimeSpan(13, 0, 0), tz);
        Assert.Equal(expectedStartUtc, ws.StartUtc);
        Assert.Equal(expectedEndUtc, ws.EndUtc);
    }

    [Fact]
    public async Task TrainerOverlap_SameTrainer_OverlappingTime_SameDate_Rejects()
    {
        var options = CreateOptions(nameof(TrainerOverlap_SameTrainer_OverlappingTime_SameDate_Rejects));
        using var db = new AppDbContext(options);
        var (adminId, trainer1Id, _) = await SetupTrainersAsync(db);
        var service = CreateService(db);

        var date = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc);
        var createReq = new AdminCreateWorkshopRequest
        {
            Title = "Conflicting Workshop",
            DanceStyle = "Contemporary",
            Level = "Advanced",
            Venue = "Hall B",
            City = "Hyderabad",
            Capacity = 30,
            Price = 500,
            WorkshopDate = date,
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(13, 0, 0),
            TrainerProfileId = trainer1Id,
            Sessions = new List<AdminWorkshopSessionItem>
            {
                new()
                {
                    SessionDate = date,
                    StartTime = new TimeSpan(10, 0, 0),
                    EndTime = new TimeSpan(12, 0, 0),
                    Capacity = 20,
                    Title = "Session A",
                    TrainerProfileId = trainer1Id
                },
                new()
                {
                    SessionDate = date,
                    StartTime = new TimeSpan(11, 30, 0),
                    EndTime = new TimeSpan(13, 0, 0),
                    Capacity = 20,
                    Title = "Session B",
                    TrainerProfileId = trainer1Id
                }
            }
        };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateWorkshopAsync(adminId, createReq, CancellationToken.None));

        Assert.Contains("overlapping sessions", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TrainerOverlap_SameTrainer_AdjacentTime_SameDate_Allowed()
    {
        var options = CreateOptions(nameof(TrainerOverlap_SameTrainer_AdjacentTime_SameDate_Allowed));
        using var db = new AppDbContext(options);
        var (adminId, trainer1Id, _) = await SetupTrainersAsync(db);
        var service = CreateService(db);

        var date = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc);
        var createReq = new AdminCreateWorkshopRequest
        {
            Title = "Back-to-Back Workshop",
            DanceStyle = "Contemporary",
            Level = "All",
            Venue = "Hall B",
            City = "Hyderabad",
            Capacity = 30,
            Price = 500,
            WorkshopDate = date,
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(13, 0, 0),
            TrainerProfileId = trainer1Id,
            Sessions = new List<AdminWorkshopSessionItem>
            {
                new()
                {
                    SessionDate = date,
                    StartTime = new TimeSpan(10, 0, 0),
                    EndTime = new TimeSpan(11, 30, 0),
                    Capacity = 20,
                    Title = "Session 1",
                    TrainerProfileId = trainer1Id
                },
                new()
                {
                    SessionDate = date,
                    StartTime = new TimeSpan(11, 30, 0),
                    EndTime = new TimeSpan(13, 0, 0),
                    Capacity = 20,
                    Title = "Session 2",
                    TrainerProfileId = trainer1Id
                }
            }
        };

        var created = await service.CreateWorkshopAsync(adminId, createReq, CancellationToken.None);
        Assert.NotNull(created);
        Assert.Equal(2, created.Sessions.Count);
    }

    [Fact]
    public async Task TrainerOverlap_SameTrainer_DifferentDates_SameTime_Allowed()
    {
        var options = CreateOptions(nameof(TrainerOverlap_SameTrainer_DifferentDates_SameTime_Allowed));
        using var db = new AppDbContext(options);
        var (adminId, trainer1Id, _) = await SetupTrainersAsync(db);
        var service = CreateService(db);

        var date1 = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc);
        var date2 = new DateTime(2026, 12, 2, 0, 0, 0, DateTimeKind.Utc);

        var createReq = new AdminCreateWorkshopRequest
        {
            Title = "Two Day Class",
            DanceStyle = "Hip Hop",
            Level = "All",
            Venue = "Hall A",
            City = "Hyderabad",
            Capacity = 30,
            Price = 500,
            WorkshopDate = date1,
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(12, 0, 0),
            TrainerProfileId = trainer1Id,
            Sessions = new List<AdminWorkshopSessionItem>
            {
                new()
                {
                    SessionDate = date1,
                    StartTime = new TimeSpan(10, 0, 0),
                    EndTime = new TimeSpan(12, 0, 0),
                    Capacity = 20,
                    Title = "Day 1 Class",
                    TrainerProfileId = trainer1Id
                },
                new()
                {
                    SessionDate = date2,
                    StartTime = new TimeSpan(10, 0, 0),
                    EndTime = new TimeSpan(12, 0, 0),
                    Capacity = 20,
                    Title = "Day 2 Class",
                    TrainerProfileId = trainer1Id
                }
            }
        };

        var created = await service.CreateWorkshopAsync(adminId, createReq, CancellationToken.None);
        Assert.NotNull(created);
        Assert.Equal(2, created.Sessions.Count);
    }

    [Fact]
    public async Task TrainerOverlap_DifferentTrainers_OverlappingTime_SameDate_Allowed()
    {
        var options = CreateOptions(nameof(TrainerOverlap_DifferentTrainers_OverlappingTime_SameDate_Allowed));
        using var db = new AppDbContext(options);
        var (adminId, trainer1Id, trainer2Id) = await SetupTrainersAsync(db);
        var service = CreateService(db);

        var date = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc);
        var createReq = new AdminCreateWorkshopRequest
        {
            Title = "Dual Track Workshop",
            DanceStyle = "All Styles",
            Level = "All",
            Venue = "Studio 1 & 2",
            City = "Hyderabad",
            Capacity = 50,
            Price = 800,
            WorkshopDate = date,
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(12, 0, 0),
            TrainerProfileId = trainer1Id,
            TrainerProfileIds = new List<Guid> { trainer1Id, trainer2Id },
            Sessions = new List<AdminWorkshopSessionItem>
            {
                new()
                {
                    SessionDate = date,
                    StartTime = new TimeSpan(10, 0, 0),
                    EndTime = new TimeSpan(12, 0, 0),
                    Capacity = 25,
                    Title = "Studio 1 - Hip Hop",
                    TrainerProfileId = trainer1Id
                },
                new()
                {
                    SessionDate = date,
                    StartTime = new TimeSpan(10, 0, 0),
                    EndTime = new TimeSpan(12, 0, 0),
                    Capacity = 25,
                    Title = "Studio 2 - Contemporary",
                    TrainerProfileId = trainer2Id
                }
            }
        };

        var created = await service.CreateWorkshopAsync(adminId, createReq, CancellationToken.None);
        Assert.NotNull(created);
        Assert.Equal(2, created.Sessions.Count);
    }

    [Fact]
    public async Task ActiveBookings_DeleteSession_WithConfirmedBooking_ThrowsInvalidOperationException()
    {
        var options = CreateOptions(nameof(ActiveBookings_DeleteSession_WithConfirmedBooking_ThrowsInvalidOperationException));
        using var db = new AppDbContext(options);
        var (adminId, trainer1Id, _) = await SetupTrainersAsync(db);
        var service = CreateService(db);

        var date = new DateTime(2026, 12, 10, 0, 0, 0, DateTimeKind.Utc);
        var ws = new Workshop
        {
            Id = Guid.NewGuid(),
            Title = "Test Protection Workshop",
            DanceStyle = "Jazz",
            Level = "Beginner",
            Venue = "Studio A",
            City = "Hyderabad",
            Price = 500,
            Capacity = 30,
            WorkshopDate = date,
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(13, 0, 0),
            TrainerProfileId = trainer1Id,
            Status = WorkshopStatus.Published,
            CreatedAt = DateTime.UtcNow
        };
        db.Workshops.Add(ws);

        var s1 = new WorkshopSession
        {
            Id = Guid.NewGuid(),
            WorkshopId = ws.Id,
            SessionDate = date,
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(11, 30, 0),
            Capacity = 30,
            Title = "Session 1",
            TrainerProfileId = trainer1Id,
            IsActive = true
        };
        var s2 = new WorkshopSession
        {
            Id = Guid.NewGuid(),
            WorkshopId = ws.Id,
            SessionDate = date,
            StartTime = new TimeSpan(11, 30, 0),
            EndTime = new TimeSpan(13, 0, 0),
            Capacity = 30,
            Title = "Session 2",
            TrainerProfileId = trainer1Id,
            IsActive = true
        };
        db.WorkshopSessions.AddRange(s1, s2);

        var booking = new WorkshopBooking
        {
            Id = Guid.NewGuid(),
            WorkshopId = ws.Id,
            StudentProfileId = Guid.NewGuid(),
            Status = WorkshopBookingStatus.Confirmed,
            BookedAt = DateTime.UtcNow
        };
        db.WorkshopBookings.Add(booking);
        db.WorkshopBookingSessions.Add(new WorkshopBookingSession
        {
            Id = Guid.NewGuid(),
            WorkshopBookingId = booking.Id,
            WorkshopSessionId = s2.Id,
            Status = WorkshopBookingSessionStatus.Booked
        });
        await db.SaveChangesAsync();

        var updateReq = new AdminUpdateWorkshopRequest
        {
            Title = ws.Title,
            DanceStyle = ws.DanceStyle,
            Level = ws.Level,
            Venue = ws.Venue,
            City = ws.City,
            Capacity = ws.Capacity,
            Price = ws.Price,
            WorkshopDate = ws.WorkshopDate,
            StartTime = ws.StartTime,
            EndTime = ws.EndTime,
            TrainerProfileId = trainer1Id,
            Sessions = new List<AdminWorkshopSessionItem>
            {
                new()
                {
                    Id = s1.Id,
                    SessionDate = s1.SessionDate,
                    StartTime = s1.StartTime,
                    EndTime = s1.EndTime,
                    Capacity = s1.Capacity,
                    Title = s1.Title,
                    TrainerProfileId = trainer1Id
                }
            }
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateWorkshopAsync(ws.Id, adminId, updateReq, CancellationToken.None));

        Assert.Contains("active customer bookings", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ActiveBookings_DeleteSession_WithActivePendingPayment_ThrowsInvalidOperationException()
    {
        var options = CreateOptions(nameof(ActiveBookings_DeleteSession_WithActivePendingPayment_ThrowsInvalidOperationException));
        using var db = new AppDbContext(options);
        var (adminId, trainer1Id, _) = await SetupTrainersAsync(db);
        var service = CreateService(db);

        var date = new DateTime(2026, 12, 10, 0, 0, 0, DateTimeKind.Utc);
        var ws = new Workshop
        {
            Id = Guid.NewGuid(),
            Title = "Pending Booking Workshop",
            DanceStyle = "Jazz",
            Level = "Beginner",
            Venue = "Studio A",
            City = "Hyderabad",
            Price = 500,
            Capacity = 30,
            WorkshopDate = date,
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(11, 30, 0),
            TrainerProfileId = trainer1Id,
            Status = WorkshopStatus.Published,
            CreatedAt = DateTime.UtcNow
        };
        db.Workshops.Add(ws);

        var s1 = new WorkshopSession
        {
            Id = Guid.NewGuid(),
            WorkshopId = ws.Id,
            SessionDate = date,
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(11, 30, 0),
            Capacity = 30,
            Title = "Session 1",
            TrainerProfileId = trainer1Id,
            IsActive = true
        };
        var s2 = new WorkshopSession
        {
            Id = Guid.NewGuid(),
            WorkshopId = ws.Id,
            SessionDate = date,
            StartTime = new TimeSpan(11, 30, 0),
            EndTime = new TimeSpan(13, 0, 0),
            Capacity = 30,
            Title = "Session 2",
            TrainerProfileId = trainer1Id,
            IsActive = true
        };
        db.WorkshopSessions.AddRange(s1, s2);

        // Pending booking with active reservation (expires in 10 minutes) on s2
        var pendingBooking = new WorkshopBooking
        {
            Id = Guid.NewGuid(),
            WorkshopId = ws.Id,
            StudentProfileId = Guid.NewGuid(),
            Status = WorkshopBookingStatus.PendingPayment,
            ReservationExpiresAt = DateTime.UtcNow.AddMinutes(10),
            BookedAt = DateTime.UtcNow
        };
        db.WorkshopBookings.Add(pendingBooking);
        db.WorkshopBookingSessions.Add(new WorkshopBookingSession
        {
            Id = Guid.NewGuid(),
            WorkshopBookingId = pendingBooking.Id,
            WorkshopSessionId = s2.Id,
            Status = WorkshopBookingSessionStatus.Booked
        });
        await db.SaveChangesAsync();

        var updateReq = new AdminUpdateWorkshopRequest
        {
            Title = ws.Title,
            DanceStyle = ws.DanceStyle,
            Level = ws.Level,
            Venue = ws.Venue,
            City = ws.City,
            Capacity = ws.Capacity,
            Price = ws.Price,
            WorkshopDate = ws.WorkshopDate,
            StartTime = ws.StartTime,
            EndTime = ws.EndTime,
            TrainerProfileId = trainer1Id,
            Sessions = new List<AdminWorkshopSessionItem>
            {
                new()
                {
                    Id = s1.Id,
                    SessionDate = s1.SessionDate,
                    StartTime = s1.StartTime,
                    EndTime = s1.EndTime,
                    Capacity = s1.Capacity,
                    Title = s1.Title,
                    TrainerProfileId = trainer1Id
                }
            }
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateWorkshopAsync(ws.Id, adminId, updateReq, CancellationToken.None));

        Assert.Contains("active customer bookings", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ActiveBookings_ChangeSessionDate_WithActiveBooking_ThrowsInvalidOperationException()
    {
        var options = CreateOptions(nameof(ActiveBookings_ChangeSessionDate_WithActiveBooking_ThrowsInvalidOperationException));
        using var db = new AppDbContext(options);
        var (adminId, trainer1Id, _) = await SetupTrainersAsync(db);
        var service = CreateService(db);

        var originalDate = new DateTime(2026, 12, 10, 0, 0, 0, DateTimeKind.Utc);
        var ws = new Workshop
        {
            Id = Guid.NewGuid(),
            Title = "Reschedule Guard Workshop",
            DanceStyle = "Ballet",
            Level = "Beginner",
            Venue = "Studio A",
            City = "Hyderabad",
            Price = 500,
            Capacity = 30,
            WorkshopDate = originalDate,
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(11, 30, 0),
            TrainerProfileId = trainer1Id,
            Status = WorkshopStatus.Published,
            CreatedAt = DateTime.UtcNow
        };
        db.Workshops.Add(ws);

        var s1 = new WorkshopSession
        {
            Id = Guid.NewGuid(),
            WorkshopId = ws.Id,
            SessionDate = originalDate,
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(11, 30, 0),
            Capacity = 30,
            Title = "Session 1",
            TrainerProfileId = trainer1Id,
            IsActive = true
        };
        db.WorkshopSessions.Add(s1);

        var booking = new WorkshopBooking
        {
            Id = Guid.NewGuid(),
            WorkshopId = ws.Id,
            StudentProfileId = Guid.NewGuid(),
            Status = WorkshopBookingStatus.Confirmed,
            BookedAt = DateTime.UtcNow
        };
        db.WorkshopBookings.Add(booking);
        db.WorkshopBookingSessions.Add(new WorkshopBookingSession
        {
            Id = Guid.NewGuid(),
            WorkshopBookingId = booking.Id,
            WorkshopSessionId = s1.Id,
            Status = WorkshopBookingSessionStatus.Booked
        });
        await db.SaveChangesAsync();

        var newDate = originalDate.AddDays(1);
        var updateReq = new AdminUpdateWorkshopRequest
        {
            Title = ws.Title,
            DanceStyle = ws.DanceStyle,
            Level = ws.Level,
            Venue = ws.Venue,
            City = ws.City,
            Capacity = ws.Capacity,
            Price = ws.Price,
            WorkshopDate = newDate,
            StartTime = ws.StartTime,
            EndTime = ws.EndTime,
            TrainerProfileId = trainer1Id,
            Sessions = new List<AdminWorkshopSessionItem>
            {
                new()
                {
                    Id = s1.Id,
                    SessionDate = newDate,
                    StartTime = s1.StartTime,
                    EndTime = s1.EndTime,
                    Capacity = s1.Capacity,
                    Title = s1.Title,
                    TrainerProfileId = trainer1Id
                }
            }
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateWorkshopAsync(ws.Id, adminId, updateReq, CancellationToken.None));

        Assert.Contains("Cannot change the date of session", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ActiveBookings_ReduceCapacity_BelowActiveBookings_ThrowsBusinessRuleException()
    {
        var options = CreateOptions(nameof(ActiveBookings_ReduceCapacity_BelowActiveBookings_ThrowsBusinessRuleException));
        using var db = new AppDbContext(options);
        var (adminId, trainer1Id, _) = await SetupTrainersAsync(db);
        var service = CreateService(db);

        var date = new DateTime(2026, 12, 10, 0, 0, 0, DateTimeKind.Utc);
        var ws = new Workshop
        {
            Id = Guid.NewGuid(),
            Title = "Capacity Guard Workshop",
            DanceStyle = "Ballet",
            Level = "Beginner",
            Venue = "Studio A",
            City = "Hyderabad",
            Price = 500,
            Capacity = 30,
            WorkshopDate = date,
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(11, 30, 0),
            TrainerProfileId = trainer1Id,
            Status = WorkshopStatus.Published,
            CreatedAt = DateTime.UtcNow
        };
        db.Workshops.Add(ws);

        var s1 = new WorkshopSession
        {
            Id = Guid.NewGuid(),
            WorkshopId = ws.Id,
            SessionDate = date,
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(11, 30, 0),
            Capacity = 30,
            Title = "Session 1",
            TrainerProfileId = trainer1Id,
            IsActive = true
        };
        db.WorkshopSessions.Add(s1);

        for (int i = 0; i < 5; i++)
        {
            var b = new WorkshopBooking
            {
                Id = Guid.NewGuid(),
                WorkshopId = ws.Id,
                StudentProfileId = Guid.NewGuid(),
                Status = WorkshopBookingStatus.Confirmed,
                BookedAt = DateTime.UtcNow
            };
            db.WorkshopBookings.Add(b);
            db.WorkshopBookingSessions.Add(new WorkshopBookingSession
            {
                Id = Guid.NewGuid(),
                WorkshopBookingId = b.Id,
                WorkshopSessionId = s1.Id,
                Status = WorkshopBookingSessionStatus.Booked
            });
        }
        await db.SaveChangesAsync();

        var updateReq = new AdminUpdateWorkshopRequest
        {
            Title = ws.Title,
            DanceStyle = ws.DanceStyle,
            Level = ws.Level,
            Venue = ws.Venue,
            City = ws.City,
            Capacity = 3,
            Price = ws.Price,
            WorkshopDate = date,
            StartTime = s1.StartTime,
            EndTime = s1.EndTime,
            TrainerProfileId = trainer1Id,
            Sessions = new List<AdminWorkshopSessionItem>
            {
                new()
                {
                    Id = s1.Id,
                    SessionDate = date,
                    StartTime = s1.StartTime,
                    EndTime = s1.EndTime,
                    Capacity = 3,
                    Title = s1.Title,
                    TrainerProfileId = trainer1Id
                }
            }
        };

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.UpdateWorkshopAsync(ws.Id, adminId, updateReq, CancellationToken.None));

        Assert.Equal("CANNOT_REDUCE_CAPACITY", ex.Code);
    }

    [Fact]
    public void UnambiguousBookingCutoff_HierarchicalFallback_Calculation()
    {
        var sessionDate = new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Unspecified);
        var startTime = new TimeSpan(18, 30, 0);
        var sessionCutoff = new TimeSpan(16, 0, 0);
        var workshopCutoff = new TimeSpan(17, 0, 0);

        var tzId = OperatingSystem.IsWindows() ? "India Standard Time" : "Asia/Kolkata";
        var tz = TimeZoneInfo.FindSystemTimeZoneById(tzId);

        // 1. Session cutoff is configured -> takes highest priority
        var wsWithCutoff = new Workshop { BookingCutoffTime = workshopCutoff };
        var session1 = new WorkshopSession
        {
            SessionDate = sessionDate,
            StartTime = startTime,
            BookingCutoffTime = sessionCutoff,
            Workshop = wsWithCutoff
        };
        var expectedSessionCutoffUtc = TimeZoneInfo.ConvertTimeToUtc(sessionDate.Date + sessionCutoff, tz);
        Assert.Equal(expectedSessionCutoffUtc, session1.GetBookingCutoffUtc("Asia/Kolkata"));

        // 2. Session cutoff is null -> falls back to workshop cutoff
        var session2 = new WorkshopSession
        {
            SessionDate = sessionDate,
            StartTime = startTime,
            BookingCutoffTime = null,
            Workshop = wsWithCutoff
        };
        var expectedWorkshopCutoffUtc = TimeZoneInfo.ConvertTimeToUtc(sessionDate.Date + workshopCutoff, tz);
        Assert.Equal(expectedWorkshopCutoffUtc, session2.GetBookingCutoffUtc("Asia/Kolkata"));

        // 3. Both are null -> falls back to session start time
        var wsWithoutCutoff = new Workshop { BookingCutoffTime = null };
        var session3 = new WorkshopSession
        {
            SessionDate = sessionDate,
            StartTime = startTime,
            BookingCutoffTime = null,
            Workshop = wsWithoutCutoff
        };
        var expectedStartFallbackUtc = TimeZoneInfo.ConvertTimeToUtc(sessionDate.Date + startTime, tz);
        Assert.Equal(expectedStartFallbackUtc, session3.GetBookingCutoffUtc("Asia/Kolkata"));
    }

    public sealed class PostgresIntegrationFactAttribute : FactAttribute
    {
        public PostgresIntegrationFactAttribute()
        {
            var pgConnection = Environment.GetEnvironmentVariable("ETHOS_TEST_POSTGRES")
                               ?? Environment.GetEnvironmentVariable("POSTGRES_TEST_CONNECTION");
            if (string.IsNullOrWhiteSpace(pgConnection))
            {
                Skip = "Requires live PostgreSQL instance via ETHOS_TEST_POSTGRES or POSTGRES_TEST_CONNECTION environment variable. Row-level FOR UPDATE serialization verified via integration facts.";
            }
        }
    }

    [PostgresIntegrationFact]
    public async Task PostgreSql_SessionCapacity_Concurrency_Serialization_IntegrationTest()
    {
        var pgConnection = Environment.GetEnvironmentVariable("ETHOS_TEST_POSTGRES")
                           ?? Environment.GetEnvironmentVariable("POSTGRES_TEST_CONNECTION")!;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(pgConnection)
            .Options;

        var workshopId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var trainerId = Guid.NewGuid();

        // 1. Setup workshop and session with 10 confirmed bookings
        using (var db = new AppDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();

            var trainerProfile = await db.TrainerProfiles.FirstOrDefaultAsync();
            if (trainerProfile == null)
            {
                var tUser = new User
                {
                    Id = Guid.NewGuid(),
                    CustomerCode = "TRN-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
                    FullName = "Test Trainer",
                    Phone = $"9188{Random.Shared.Next(10000000, 99999999)}",
                    Email = $"trainer_{Guid.NewGuid():N}@ethos.test",
                    PasswordHash = "hash",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                db.Users.Add(tUser);

                var tier = await db.TrainerTiers.FirstOrDefaultAsync() ?? new TrainerTier { Id = Guid.NewGuid(), Name = "Core", Code = "CORE" };
                if (db.Entry(tier).State == EntityState.Detached) db.TrainerTiers.Add(tier);

                trainerProfile = new TrainerProfile
                {
                    Id = Guid.NewGuid(),
                    UserId = tUser.Id,
                    CurrentTierId = tier.Id,
                    TrainerCode = "TRN-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
                    FullName = "Test Trainer",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                db.TrainerProfiles.Add(trainerProfile);
                await db.SaveChangesAsync();
            }
            trainerId = trainerProfile.Id;

            var studentProfile = await db.StudentProfiles.FirstOrDefaultAsync();
            if (studentProfile == null)
            {
                var sUser = new User
                {
                    Id = Guid.NewGuid(),
                    CustomerCode = "STU-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
                    FullName = "Test Student",
                    Phone = $"9199{Random.Shared.Next(10000000, 99999999)}",
                    Email = $"student_{Guid.NewGuid():N}@ethos.test",
                    PasswordHash = "hash",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                db.Users.Add(sUser);

                studentProfile = new StudentProfile
                {
                    Id = Guid.NewGuid(),
                    UserId = sUser.Id,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                db.StudentProfiles.Add(studentProfile);
                await db.SaveChangesAsync();
            }

            var workshop = new Workshop
            {
                Id = workshopId,
                Title = "PG Multi-Connection Concurrency Test",
                DanceStyle = "Hip Hop",
                Level = "All",
                Venue = "Main Studio",
                City = "Hyderabad",
                Status = WorkshopStatus.Published,
                Capacity = 30,
                WorkshopDate = DateTime.UtcNow.AddDays(7),
                StartTime = new TimeSpan(10, 0, 0),
                EndTime = new TimeSpan(11, 30, 0),
                CreatedAt = DateTime.UtcNow
            };
            db.Workshops.Add(workshop);

            var session = new WorkshopSession
            {
                Id = sessionId,
                WorkshopId = workshopId,
                Title = "Locked Session",
                SessionDate = DateTime.UtcNow.AddDays(7),
                StartTime = new TimeSpan(10, 0, 0),
                EndTime = new TimeSpan(11, 30, 0),
                TrainerProfileId = trainerId,
                Capacity = 20,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.WorkshopSessions.Add(session);

            for (int i = 0; i < 10; i++)
            {
                var b = new WorkshopBooking
                {
                    Id = Guid.NewGuid(),
                    WorkshopId = workshopId,
                    StudentProfileId = studentProfile.Id,
                    Status = WorkshopBookingStatus.Confirmed,
                    BookedAt = DateTime.UtcNow
                };
                db.WorkshopBookings.Add(b);
                db.WorkshopBookingSessions.Add(new WorkshopBookingSession
                {
                    Id = Guid.NewGuid(),
                    WorkshopBookingId = b.Id,
                    WorkshopSessionId = sessionId,
                    Status = WorkshopBookingSessionStatus.Booked
                });
            }

            await db.SaveChangesAsync();
        }

        // 2. Multi-connection concurrency test:
        // Connection A acquires FOR UPDATE lock, updates capacity to 15, and holds lock.
        // Connection B attempts FOR UPDATE on the same row, is physically blocked/serialized by PostgreSQL,
        // and upon Connection A's commit, unblocks, observes committed capacity = 15, and tests capacity reduction guard.
        var lockAcquiredTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopwatch = new System.Diagnostics.Stopwatch();
        int observedCapacityByConnectionB = 0;
        Exception? connectionBException = null;

        var taskA = Task.Run(async () =>
        {
            using var dbA = new AppDbContext(options);
            using var txA = await dbA.Database.BeginTransactionAsync();

            // Acquire exclusive FOR UPDATE lock on session
            await dbA.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT \"Id\" FROM workshop_sessions WHERE \"Id\" = {sessionId} FOR UPDATE");

            var sessionA = await dbA.WorkshopSessions.SingleAsync(s => s.Id == sessionId);

            // Signal to Connection B that lock is held
            lockAcquiredTcs.SetResult(true);

            // Allow Connection B time to reach and block on FOR UPDATE in PostgreSQL
            await Task.Delay(150);

            // Connection A updates capacity to 15 (>= 10 booked seats)
            sessionA.Capacity = 15;
            sessionA.UpdatedAt = DateTime.UtcNow;
            await dbA.SaveChangesAsync();

            // Hold lock for another 250ms to demonstrate physical serialization
            await Task.Delay(250);

            await txA.CommitAsync();
        });

        var taskB = Task.Run(async () =>
        {
            // Wait until Connection A has acquired the row lock
            await lockAcquiredTcs.Task;

            using var dbB = new AppDbContext(options);
            using var txB = await dbB.Database.BeginTransactionAsync();

            stopwatch.Start();

            // This call physically blocks inside PostgreSQL until Connection A commits
            await dbB.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT \"Id\" FROM workshop_sessions WHERE \"Id\" = {sessionId} FOR UPDATE");

            stopwatch.Stop();

            // Connection B unblocks after Connection A committed
            var sessionB = await dbB.WorkshopSessions.AsNoTracking().SingleAsync(s => s.Id == sessionId);
            observedCapacityByConnectionB = sessionB.Capacity;

            // Connection B verifies capacity reduction guard: reducing below 10 must throw
            var serviceB = new AdminWorkshopService(dbB, new DummyAuditService());
            try
            {
                await serviceB.UpdateSessionAsync(workshopId, sessionId, 8, CancellationToken.None);
            }
            catch (Exception ex)
            {
                connectionBException = ex;
            }

            await txB.CommitAsync();
        });

        try
        {
            await Task.WhenAll(taskA, taskB);

            // Verification:
            // 1. Connection B was physically blocked for at least 200ms
            Assert.True(stopwatch.ElapsedMilliseconds >= 200,
                $"Connection B should have been blocked waiting for Connection A's lock. Elapsed: {stopwatch.ElapsedMilliseconds}ms");

            // 2. Connection B observed the updated capacity committed by Connection A
            Assert.Equal(15, observedCapacityByConnectionB);

            // 3. Reducing capacity below 10 booked seats threw CANNOT_REDUCE_CAPACITY
            Assert.NotNull(connectionBException);
            var bre = Assert.IsType<BusinessRuleException>(connectionBException);
            Assert.Equal("CANNOT_REDUCE_CAPACITY", bre.Code);
        }
        finally
        {
            using var cleanupDb = new AppDbContext(options);
            var bookingSessions = await cleanupDb.WorkshopBookingSessions.Where(bs => bs.WorkshopSessionId == sessionId).ToListAsync();
            cleanupDb.WorkshopBookingSessions.RemoveRange(bookingSessions);
            var bookings = await cleanupDb.WorkshopBookings.Where(b => b.WorkshopId == workshopId).ToListAsync();
            cleanupDb.WorkshopBookings.RemoveRange(bookings);
            var sessions = await cleanupDb.WorkshopSessions.Where(s => s.WorkshopId == workshopId).ToListAsync();
            cleanupDb.WorkshopSessions.RemoveRange(sessions);
            var ws = await cleanupDb.Workshops.FindAsync(workshopId);
            if (ws != null) cleanupDb.Workshops.Remove(ws);
            await cleanupDb.SaveChangesAsync();
        }
    }
}

