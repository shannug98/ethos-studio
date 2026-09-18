using Ethos.Api.Application.Auth;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Ethos.Api.Application.Admin;

public static class Phase2SeedService
{
    public static async Task SeedAsync(
        AppDbContext db,
        IPasswordService? passwordService = null,
        IConfiguration? configuration = null,
        bool isDevelopment = true)
    {
        // 1. Ensure ADMIN role exists
        var adminRole = await db.Roles.FirstOrDefaultAsync(r => r.Code == "ADMIN");
        if (adminRole == null)
        {
            adminRole = new Role
            {
                Id = Guid.NewGuid(),
                Code = "ADMIN",
                Name = "Administrator",
                Description = "System Administrator with full access rights"
            };
            db.Roles.Add(adminRole);
            await db.SaveChangesAsync();
        }

        // 2. Ensure Authorized Partner Admin Users (8019013757 & 8341701113) have ADMIN role
        var bootstrapPassword = configuration?["Admin:BootstrapPassword"]
            ?? Environment.GetEnvironmentVariable("ETHOS_ADMIN_BOOTSTRAP_PASSWORD");

        var authorizedAdmins = new (string Phone, string CustomerCode, string FullName)[]
        {
            ("8019013757", "ETHADMIN001", "Ethos Partner 1"),
            ("8341701113", "ETHADMIN002", "Ethos Partner 2")
        };

        foreach (var (phone, customerCode, fullName) in authorizedAdmins)
        {
            var adminUser = await db.Users
                .Include(u => u.UserRoles)
                .FirstOrDefaultAsync(u => u.Phone == phone);

            if (adminUser == null)
            {
                if (string.IsNullOrWhiteSpace(bootstrapPassword))
                {
                    throw new InvalidOperationException(
                        $"Admin initialization failed for {phone}: ETHOS_ADMIN_BOOTSTRAP_PASSWORD environment variable or Admin:BootstrapPassword configuration is required to initialize an uninitialized environment.");
                }

                var initialHash = passwordService != null
                    ? passwordService.HashPassword(bootstrapPassword)
                    : new Microsoft.AspNetCore.Identity.PasswordHasher<User>().HashPassword(null!, bootstrapPassword);

                adminUser = new User
                {
                    Id = Guid.NewGuid(),
                    CustomerCode = customerCode,
                    FullName = fullName,
                    Phone = phone,
                    PasswordHash = initialHash,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                adminUser.UserRoles.Add(new UserRole
                {
                    Id = Guid.NewGuid(),
                    UserId = adminUser.Id,
                    RoleId = adminRole.Id,
                    AssignedAt = DateTime.UtcNow
                });
                db.Users.Add(adminUser);
                await db.SaveChangesAsync();
            }
            else
            {
                adminUser.IsActive = true;
                adminUser.CustomerCode = customerCode;
                adminUser.FullName = fullName;

                // Only set PasswordHash if it was previously empty/uninitialized
                if (string.IsNullOrWhiteSpace(adminUser.PasswordHash))
                {
                    if (string.IsNullOrWhiteSpace(bootstrapPassword))
                    {
                        throw new InvalidOperationException(
                            $"Admin initialization failed for {phone}: ETHOS_ADMIN_BOOTSTRAP_PASSWORD environment variable or Admin:BootstrapPassword configuration is required to set an initial password for uninitialized admin account.");
                    }

                    var initialHash = passwordService != null
                        ? passwordService.HashPassword(bootstrapPassword)
                        : new Microsoft.AspNetCore.Identity.PasswordHasher<User>().HashPassword(null!, bootstrapPassword);

                    adminUser.PasswordHash = initialHash;
                }

                if (!adminUser.UserRoles.Any(ur => ur.RoleId == adminRole.Id))
                {
                    db.UserRoles.Add(new UserRole
                    {
                        Id = Guid.NewGuid(),
                        UserId = adminUser.Id,
                        RoleId = adminRole.Id,
                        AssignedAt = DateTime.UtcNow
                    });
                }
                await db.SaveChangesAsync();
            }
        }

        // Deactivate deprecated test admin 9999999999 if present
        var legacyAdmin = await db.Users.FirstOrDefaultAsync(u => u.Phone == "9999999999");
        if (legacyAdmin != null)
        {
            legacyAdmin.IsActive = false;
            await db.SaveChangesAsync();
        }

        // 3. Seed Workshop-Focused Permissions
        var permissionDefs = new (string Code, string Name, string Description)[]
        {
            ("TRAINER_VIEW_PROFILE", "View Profile", "Access trainer profile details"),
            ("TRAINER_UPDATE_PROFILE", "Update Profile", "Edit trainer profile details"),
            ("TRAINER_VIEW_TIER", "View Tier Details", "Access tier and permission info"),
            ("TRAINER_REQUEST_TIER_UPGRADE", "Request Upgrade", "Submit tier upgrade requests"),
            ("TRAINER_VIEW_WORKSHOPS", "View Workshops", "Access trainer workshops"),
            ("TRAINER_CREATE_WORKSHOP", "Create Workshop", "Submit new workshop proposals"),
            ("TRAINER_UPDATE_WORKSHOP", "Update Workshop", "Edit workshop details"),
            ("TRAINER_CANCEL_WORKSHOP", "Cancel Workshop", "Cancel workshop sessions"),
            ("TRAINER_VIEW_WORKSHOP_STUDENTS", "View Workshop Students", "Access student roster for workshops"),
            ("TRAINER_VIEW_WORKSHOP_FEEDBACK", "View Workshop Feedback", "Access student feedback for workshops"),
            ("TRAINER_VIEW_PERFORMANCE", "View Performance", "Access performance analytics"),
            ("TRAINER_VIEW_AVAILABILITY", "View Availability", "Access weekly availability schedule"),
            ("TRAINER_UPDATE_AVAILABILITY", "Update Availability", "Manage weekly availability schedule"),
            ("TRAINER_VIEW_NOTIFICATIONS", "View Notifications", "Access in-app notifications")
        };

        var dbPermissions = await db.Permissions.ToListAsync();
        foreach (var def in permissionDefs)
        {
            if (!dbPermissions.Any(p => p.Code == def.Code))
            {
                var p = new Permission
                {
                    Id = Guid.NewGuid(),
                    Code = def.Code,
                    Name = def.Name,
                    Description = def.Description,
                    IsActive = true
                };
                db.Permissions.Add(p);
                dbPermissions.Add(p);
            }
        }
        await db.SaveChangesAsync();

        // 4. Seed Tiers (Silver, Gold, Diamond, Platinum)
        var tierDefs = new (string Code, string Name, int DisplayOrder, decimal? ApplicationFee, decimal? UpgradeFee, string Description)[]
        {
            ("SILVER", "Silver Trainer", 1, 1000m, 1500m, "Entry level trainer with standard workshop permissions"),
            ("GOLD", "Gold Trainer", 2, 2500m, 3000m, "Intermediate trainer with priority workshop scheduling"),
            ("DIAMOND", "Diamond Trainer", 3, 5000m, 6000m, "Advanced trainer with featured workshop placement"),
            ("PLATINUM", "Platinum Master", 4, 10000m, 0m, "Master trainer with unlimited privileges")
        };

        var dbTiers = await db.TrainerTiers.Include(t => t.Permissions).ToListAsync();
        foreach (var def in tierDefs)
        {
            var tier = dbTiers.FirstOrDefault(t => t.Code == def.Code);
            if (tier == null)
            {
                tier = new TrainerTier
                {
                    Id = Guid.NewGuid(),
                    Code = def.Code,
                    Name = def.Name,
                    DisplayOrder = def.DisplayOrder,
                    ApplicationFee = def.ApplicationFee,
                    UpgradeFee = def.UpgradeFee,
                    Description = def.Description,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                db.TrainerTiers.Add(tier);
                dbTiers.Add(tier);
            }
            else
            {
                tier.ApplicationFee = def.ApplicationFee;
                tier.UpgradeFee = def.UpgradeFee;
            }
        }
        await db.SaveChangesAsync();

        // 5. Seed Permission Matrix
        var silver = dbTiers.First(t => t.Code == "SILVER");
        var gold = dbTiers.First(t => t.Code == "GOLD");
        var diamond = dbTiers.First(t => t.Code == "DIAMOND");
        var platinum = dbTiers.First(t => t.Code == "PLATINUM");

        var permMap = dbPermissions.ToDictionary(p => p.Code);

        void EnsureMatrix(TrainerTier tier, string permCode, bool allowed)
        {
            if (permMap.TryGetValue(permCode, out var p))
            {
                var existing = db.TrainerTierPermissions
                    .FirstOrDefault(tp => tp.TrainerTierId == tier.Id && tp.PermissionId == p.Id);

                if (existing == null)
                {
                    db.TrainerTierPermissions.Add(new TrainerTierPermission
                    {
                        TrainerTierId = tier.Id,
                        PermissionId = p.Id,
                        IsAllowed = allowed
                    });
                }
                else
                {
                    existing.IsAllowed = allowed;
                }
            }
        }

        foreach (var p in dbPermissions)
        {
            bool isUpgradePerm = p.Code == "TRAINER_REQUEST_TIER_UPGRADE";

            EnsureMatrix(silver, p.Code, true);
            EnsureMatrix(gold, p.Code, true);
            EnsureMatrix(diamond, p.Code, true);
            EnsureMatrix(platinum, p.Code, !isUpgradePerm);
        }

        await db.SaveChangesAsync();

        // 6. Ensure TRAINER role exists & test trainer user (phone 8686759209) has TRAINER role & TrainerProfile
        var trainerRole = await db.Roles.FirstOrDefaultAsync(r => r.Code == "TRAINER");
        if (trainerRole == null)
        {
            trainerRole = new Role
            {
                Id = Guid.NewGuid(),
                Code = "TRAINER",
                Name = "Trainer",
                Description = "Studio Trainer with workshop and availability management privileges"
            };
            db.Roles.Add(trainerRole);
            await db.SaveChangesAsync();
        }

        var shouldSeedDevData = isDevelopment && configuration?.GetValue<bool>("Seed:EnableTestData", false) == true;
        if (shouldSeedDevData)
        {
            var trainerUser = await db.Users
                .Include(u => u.UserRoles)
                .FirstOrDefaultAsync(u => u.Phone == "8686759209");

            if (trainerUser == null)
            {
                trainerUser = new User
                {
                    Id = Guid.NewGuid(),
                    CustomerCode = "ETHTR001",
                    FullName = "Shanmuka Trainer",
                    Phone = "8686759209",
                    Email = "shanmuka@ethosdance.com",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                trainerUser.UserRoles.Add(new UserRole
                {
                    Id = Guid.NewGuid(),
                    UserId = trainerUser.Id,
                    RoleId = trainerRole.Id,
                    AssignedAt = DateTime.UtcNow
                });
                db.Users.Add(trainerUser);
                await db.SaveChangesAsync();
            }
            else if (!trainerUser.UserRoles.Any(ur => ur.RoleId == trainerRole.Id))
            {
                db.UserRoles.Add(new UserRole
                {
                    Id = Guid.NewGuid(),
                    UserId = trainerUser.Id,
                    RoleId = trainerRole.Id,
                    AssignedAt = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
            }

            if (string.IsNullOrWhiteSpace(trainerUser.PasswordHash))
            {
                var passwordHasher = new Microsoft.AspNetCore.Identity.PasswordHasher<User>();
                trainerUser.PasswordHash = passwordHasher.HashPassword(trainerUser, "Trainer@123");
                trainerUser.MustChangePassword = false;
                await db.SaveChangesAsync();
            }

            var trainerProfile = await db.TrainerProfiles.FirstOrDefaultAsync(tp => tp.UserId == trainerUser.Id);
            if (trainerProfile == null)
            {
                var silverTier = await db.TrainerTiers.FirstOrDefaultAsync(t => t.Code == "SILVER");
                db.TrainerProfiles.Add(new TrainerProfile
                {
                    Id = Guid.NewGuid(),
                    UserId = trainerUser.Id,
                    TrainerCode = "ETH-TR-001",
                    FullName = trainerUser.FullName,
                    City = "Hyderabad",
                    PrimaryDanceStyle = "Hip Hop",
                    ExperienceYears = 5,
                    CurrentStudio = "Ethos Main Studio",
                    Bio = "Senior Hip-Hop instructor with 5+ years of choreography and training experience.",
                    Status = TrainerStatus.Active,
                    CurrentTierId = silverTier?.Id,
                    ApprovedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
            }

            var newTrainerUser = await db.Users.Include(u => u.UserRoles).FirstOrDefaultAsync(u => u.Phone == "5555566666");
            if (newTrainerUser == null)
            {
                newTrainerUser = new User
                {
                    Id = Guid.NewGuid(),
                    CustomerCode = "ETHTR002",
                    FullName = "Rahul Sharma",
                    Phone = "5555566666",
                    Email = "rahul@ethosdance.com",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                newTrainerUser.UserRoles.Add(new UserRole
                {
                    Id = Guid.NewGuid(),
                    UserId = newTrainerUser.Id,
                    RoleId = trainerRole.Id,
                    AssignedAt = DateTime.UtcNow
                });
                db.Users.Add(newTrainerUser);
                await db.SaveChangesAsync();
            }
            else if (!newTrainerUser.UserRoles.Any(ur => ur.RoleId == trainerRole.Id))
            {
                db.UserRoles.Add(new UserRole
                {
                    Id = Guid.NewGuid(),
                    UserId = newTrainerUser.Id,
                    RoleId = trainerRole.Id,
                    AssignedAt = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
            }

            {
                newTrainerUser.FullName = "Rahul Sharma";
                var passwordHasher = new Microsoft.AspNetCore.Identity.PasswordHasher<User>();
                newTrainerUser.PasswordHash = passwordHasher.HashPassword(newTrainerUser, "EthosTemp#9999");
                newTrainerUser.MustChangePassword = true;
                newTrainerUser.UpdatedAt = DateTime.UtcNow;

                var newTrainerProfile = await db.TrainerProfiles.FirstOrDefaultAsync(tp => tp.UserId == newTrainerUser.Id);
                if (newTrainerProfile == null)
                {
                    var silverTier = await db.TrainerTiers.FirstOrDefaultAsync(t => t.Code == "SILVER");
                    db.TrainerProfiles.Add(new TrainerProfile
                    {
                        Id = Guid.NewGuid(),
                        UserId = newTrainerUser.Id,
                        TrainerCode = "ETH-TR-" + Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper(),
                        FullName = "Rahul Sharma",
                        City = "Mumbai",
                        PrimaryDanceStyle = "Contemporary",
                        ExperienceYears = 3,
                        CurrentStudio = "Ethos West Studio",
                        Bio = "Contemporary movement artist & instructor.",
                        Status = TrainerStatus.Active,
                        CurrentTierId = silverTier?.Id,
                        ApprovedAt = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
                await db.SaveChangesAsync();
            }

            // 4. Seed Starter Dance Classes, Schedules, and Upcoming Sessions
            if (isDevelopment)
            {
                var urbanChoreo = await db.DanceClasses.FirstOrDefaultAsync(c => c.Name == "Urban Choreography - Beginner");
                if (urbanChoreo == null)
                {
                    Console.WriteLine("[SEED] Seeding starter dance classes, schedules, and sessions...");
                    var classes = new List<DanceClass>
                    {
                        new DanceClass
                        {
                            Id = Guid.NewGuid(),
                            Name = "Urban Choreography - Beginner",
                            Description = "Foundational urban choreography, groove dynamics, and musicality for beginners.",
                            DanceStyle = "Urban Choreography",
                            Level = "Beginner",
                            DurationMinutes = 90,
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        },
                        new DanceClass
                        {
                            Id = Guid.NewGuid(),
                            Name = "Contemporary Flow - Intermediate",
                            Description = "Expressive contemporary flow focusing on floor work, release technique, and expressive movement.",
                            DanceStyle = "Contemporary",
                            Level = "Intermediate",
                            DurationMinutes = 90,
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        },
                        new DanceClass
                        {
                            Id = Guid.NewGuid(),
                            Name = "Hip-Hop Fundamentals - All Levels",
                            Description = "Core bouncing, party dances, isolations, and freestyle rhythm for all levels.",
                            DanceStyle = "Hip-Hop",
                            Level = "All Levels",
                            DurationMinutes = 90,
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        }
                    };

                    db.DanceClasses.AddRange(classes);
                    await db.SaveChangesAsync();

                    // Seed schedules for each class
                    var schedules = new List<ClassSchedule>
                    {
                        // Urban: Wednesday 18:00 & Friday 18:00
                        new ClassSchedule
                        {
                            Id = Guid.NewGuid(),
                            DanceClassId = classes[0].Id,
                            DayOfWeek = DayOfWeek.Wednesday,
                            StartTime = new TimeSpan(18, 0, 0),
                            EndTime = new TimeSpan(19, 30, 0),
                            StudioRoom = "Studio A (Main Hall)",
                            Capacity = 30,
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        },
                        new ClassSchedule
                        {
                            Id = Guid.NewGuid(),
                            DanceClassId = classes[0].Id,
                            DayOfWeek = DayOfWeek.Friday,
                            StartTime = new TimeSpan(18, 0, 0),
                            EndTime = new TimeSpan(19, 30, 0),
                            StudioRoom = "Studio A (Main Hall)",
                            Capacity = 30,
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        },
                        // Contemporary: Tuesday 19:00 & Thursday 19:00
                        new ClassSchedule
                        {
                            Id = Guid.NewGuid(),
                            DanceClassId = classes[1].Id,
                            DayOfWeek = DayOfWeek.Tuesday,
                            StartTime = new TimeSpan(19, 0, 0),
                            EndTime = new TimeSpan(20, 30, 0),
                            StudioRoom = "Studio B (Mirrors)",
                            Capacity = 25,
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        },
                        new ClassSchedule
                        {
                            Id = Guid.NewGuid(),
                            DanceClassId = classes[1].Id,
                            DayOfWeek = DayOfWeek.Thursday,
                            StartTime = new TimeSpan(19, 0, 0),
                            EndTime = new TimeSpan(20, 30, 0),
                            StudioRoom = "Studio B (Mirrors)",
                            Capacity = 25,
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        },
                        // Hip-Hop: Saturday 11:00 & Sunday 11:00
                        new ClassSchedule
                        {
                            Id = Guid.NewGuid(),
                            DanceClassId = classes[2].Id,
                            DayOfWeek = DayOfWeek.Saturday,
                            StartTime = new TimeSpan(11, 0, 0),
                            EndTime = new TimeSpan(12, 30, 0),
                            StudioRoom = "Studio A (Main Hall)",
                            Capacity = 35,
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        },
                        new ClassSchedule
                        {
                            Id = Guid.NewGuid(),
                            DanceClassId = classes[2].Id,
                            DayOfWeek = DayOfWeek.Sunday,
                            StartTime = new TimeSpan(11, 0, 0),
                            EndTime = new TimeSpan(12, 30, 0),
                            StudioRoom = "Studio A (Main Hall)",
                            Capacity = 35,
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        }
                    };

                    db.ClassSchedules.AddRange(schedules);
                    await db.SaveChangesAsync();

                    // Pre-generate sessions for the next 14 days
                    var today = DateTime.UtcNow.Date;
                    var sessions = new List<ClassSession>();

                    foreach (var schedule in schedules)
                    {
                        for (int i = 0; i < 14; i++)
                        {
                            var targetDate = today.AddDays(i);
                            if (targetDate.DayOfWeek == schedule.DayOfWeek)
                            {
                                sessions.Add(new ClassSession
                                {
                                    Id = Guid.NewGuid(),
                                    ClassScheduleId = schedule.Id,
                                    SessionDate = DateTime.SpecifyKind(targetDate, DateTimeKind.Utc),
                                    StartTime = schedule.StartTime,
                                    EndTime = schedule.EndTime,
                                    Status = ClassSessionStatus.Scheduled,
                                    CreatedAt = DateTime.UtcNow,
                                    UpdatedAt = DateTime.UtcNow
                                });
                            }
                        }
                    }

                    db.ClassSessions.AddRange(sessions);
                    await db.SaveChangesAsync();
                    Console.WriteLine("[SEED] Starter dance classes and sessions seeded successfully.");
                }

                // 5. Seed Starter Approved Workshops
                var existingWorkshops = await db.Workshops.AnyAsync(w => w.Status == WorkshopStatus.Approved);
                if (!existingWorkshops)
                {
                    Console.WriteLine("[SEED] Seeding starter approved workshops...");
                    var trainer1 = await db.TrainerProfiles.FirstOrDefaultAsync(tp => tp.FullName == "Shanmuka Trainer");
                    var trainer2 = await db.TrainerProfiles.FirstOrDefaultAsync(tp => tp.FullName == "Rahul Sharma");

                    var now = DateTime.UtcNow;
                    var seedWorkshops = new List<Workshop>
                    {
                        new Workshop
                        {
                            Id = Guid.NewGuid(),
                            TrainerProfileId = trainer1?.Id,
                            Title = "Urban Dance Intensive",
                            Description = "A masterclass focusing on high-speed urban execution, body dynamics, textures, and stage presence.",
                            DanceStyle = "Urban / Hip-Hop",
                            Level = "All Levels",
                            WorkshopDate = DateTime.SpecifyKind(now.Date.AddDays(7), DateTimeKind.Utc),
                            StartTime = new TimeSpan(16, 0, 0),
                            EndTime = new TimeSpan(19, 0, 0),
                            Venue = "Studio A (Main Hall), Ethos Studio",
                            Price = 1000m,
                            TrainerProposedPrice = 1000m,
                            AdminApprovedPrice = 1000m,
                            PriceApprovedAt = now,
                            Capacity = 30,
                            Status = WorkshopStatus.Approved,
                            ImageUrl = "/assets/workshops/workshop-01.jpg",
                            CreatedAt = now,
                            UpdatedAt = now
                        },
                        new Workshop
                        {
                            Id = Guid.NewGuid(),
                            TrainerProfileId = trainer2?.Id,
                            Title = "Contemporary Movement Flow",
                            Description = "Immerse into floorwork mechanics, seamless transitions, musical nuance, and expressive storytelling.",
                            DanceStyle = "Contemporary",
                            Level = "Intermediate",
                            WorkshopDate = DateTime.SpecifyKind(now.Date.AddDays(12), DateTimeKind.Utc),
                            StartTime = new TimeSpan(17, 0, 0),
                            EndTime = new TimeSpan(20, 0, 0),
                            Venue = "Studio B, Ethos Studio",
                            Price = 1200m,
                            TrainerProposedPrice = 1200m,
                            AdminApprovedPrice = 1200m,
                            PriceApprovedAt = now,
                            Capacity = 25,
                            Status = WorkshopStatus.Approved,
                            ImageUrl = "/assets/workshops/workshop-02.jpg",
                            CreatedAt = now,
                            UpdatedAt = now
                        },
                        new Workshop
                        {
                            Id = Guid.NewGuid(),
                            TrainerProfileId = trainer1?.Id,
                            Title = "Freestyle & Battle Immersion",
                            Description = "Master battle mindset, spontaneous musical improvisation, groove vocabulary, and live cypher confidence.",
                            DanceStyle = "Freestyle / Grooves",
                            Level = "Open to All",
                            WorkshopDate = DateTime.SpecifyKind(now.Date.AddDays(18), DateTimeKind.Utc),
                            StartTime = new TimeSpan(15, 0, 0),
                            EndTime = new TimeSpan(18, 30, 0),
                            Venue = "Studio A (Main Hall), Ethos Studio",
                            Price = 1500m,
                            TrainerProposedPrice = 1500m,
                            AdminApprovedPrice = 1500m,
                            PriceApprovedAt = now,
                            Capacity = 40,
                            Status = WorkshopStatus.Approved,
                            ImageUrl = "/assets/workshops/workshop-03.jpg",
                            CreatedAt = now,
                            UpdatedAt = now
                        }
                    };

                    db.Workshops.AddRange(seedWorkshops);
                    await db.SaveChangesAsync();
                    Console.WriteLine("[SEED] Starter approved workshops seeded successfully.");
                }
            }
        }
    }
}
