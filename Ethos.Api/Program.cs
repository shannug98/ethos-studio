using System.Globalization;
using System.Net;
using System.Text;
using System.Threading.RateLimiting;
using Ethos.Api.Application.Admin;
using Ethos.Api.Application.Attendance;
using Ethos.Api.Application.Auth;
using Ethos.Api.Application.Classes;
using Ethos.Api.Application.Dashboard;
using Ethos.Api.Application.Notifications;
using Ethos.Api.Application.Packages;
using Ethos.Api.Application.Payments;
using Ethos.Api.Application.Students;
using Ethos.Api.Application.Trainers;
using Ethos.Api.Application.Storage;
using Ethos.Api.Application.Feedback;
using Ethos.Api.Application.Finance;
using Ethos.Api.Application.Workshops;
using Ethos.Api.Application.Echo;
using Ethos.Api.Application.Common;
using Ethos.Api.Application.Media;
using Ethos.Api.Infrastructure.Storage;
using Ethos.Api.Infrastructure.Telemetry;
using Ethos.Api.Domain.Authentication;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Payment;
using Ethos.Api.Infrastructure.Authentication;
using Ethos.Api.Infrastructure.Notifications;
using Ethos.Api.Infrastructure.Persistence;
using Ethos.Api.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.FileProviders;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto;

    options.ForwardLimit = 1;

    var configuredProxies =
        builder.Configuration
            .GetSection("ForwardedHeaders:KnownProxies")
            .Get<string[]>();

    if (configuredProxies is not null)
    {
        foreach (var proxy in configuredProxies)
        {
            if (IPAddress.TryParse(proxy, out var proxyAddress))
            {
                options.KnownProxies.Add(proxyAddress);
            }
        }
    }
});

builder.Services.AddCors(options =>
{
    var configuredOrigins =
        builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>()
            ?? Array.Empty<string>();

    var allowedOrigins = configuredOrigins
        .Where(origin => !string.IsNullOrWhiteSpace(origin))
        .Select(origin => origin.Trim().TrimEnd('/'))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    options.AddDefaultPolicy(policy =>
    {
        policy
            .SetIsOriginAllowed(origin =>
            {
                if (string.IsNullOrWhiteSpace(origin)) return false;

                if (builder.Environment.IsDevelopment())
                {
                    return origin.StartsWith("http://localhost:", StringComparison.OrdinalIgnoreCase)
                        || origin.StartsWith("http://127.0.0.1:", StringComparison.OrdinalIgnoreCase);
                }

                if (Uri.TryCreate(origin, UriKind.Absolute, out _))
                {
                    return allowedOrigins.Contains(origin.TrimEnd('/'), StringComparer.OrdinalIgnoreCase);
                }

                return false;
            })
            .AllowAnyHeader()
            .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
            .AllowCredentials()
            .WithExposedHeaders("x-trace-id");
    });
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = async (context, cancellationToken) =>
    {
        if (context.Lease.TryGetMetadata(
                MetadataName.RetryAfter,
                out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)Math.Ceiling(retryAfter.TotalSeconds))
                .ToString(CultureInfo.InvariantCulture);
        }

        context.HttpContext.Response.ContentType =
            "application/problem+json";

        await context.HttpContext.Response.WriteAsJsonAsync(
            new
            {
                type = "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.9",
                title = "Too Many Requests",
                status = StatusCodes.Status429TooManyRequests,
                detail = "Too many requests. Please try again later."
            },
            options: null,
            contentType: "application/problem+json",
            cancellationToken: cancellationToken);
    };

    options.GlobalLimiter =
        PartitionedRateLimiter.Create<HttpContext, string>(context =>
        {
            var path = context.Request.Path;

            // Auth endpoints: 20 req/min/IP
            if (path.StartsWithSegments("/api/auth"))
            {
                var ip =
                    context.Connection.RemoteIpAddress?.ToString()
                    ?? "unknown";

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: $"auth-ip:{ip}",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 20,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
            }

            // Public payment endpoints (unauthenticated): 10 req/min/IP
            if (path.StartsWithSegments("/api/payments/public"))
            {
                var ip =
                    context.Connection.RemoteIpAddress?.ToString()
                    ?? "unknown";

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: $"public-payment-ip:{ip}",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
            }


            // Workshop ticket validation & check-in endpoints: 60 req/min/IP to prevent brute force / DoS
            if (path.Value != null && path.Value.Contains("/tickets/validate", StringComparison.OrdinalIgnoreCase))
            {
                var ip =
                    context.Connection.RemoteIpAddress?.ToString()
                    ?? "unknown";

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: $"ticket-validate-ip:{ip}",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 60,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
            }

            return RateLimitPartition.GetNoLimiter("no-limit");
        });
});

builder.Services.AddControllers(options =>
{
    options.Filters.Add<AdminSessionValidationFilter>();
})
.AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(connectionString);
    options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
});

builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection("Jwt"));

builder.Services
    .AddOptions<RazorpaySettings>()
    .Bind(builder.Configuration.GetSection("Razorpay"))
    .Validate(
        settings =>
            !string.IsNullOrWhiteSpace(settings.KeyId) &&
            !string.IsNullOrWhiteSpace(settings.KeySecret),
        "Razorpay KeyId and KeySecret are required.")
    .ValidateOnStart();

builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<IOtpService, OtpService>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPasswordService, PasswordService>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IStudentEligibilityService, StudentEligibilityService>();
builder.Services.AddScoped<IStudentProfilePhotoStorageService, LocalStudentProfilePhotoStorageService>();
builder.Services.AddScoped<IStudentProfileService, StudentProfileService>();
builder.Services.AddScoped<IPackageService, PackageService>();
builder.Services.AddScoped<IDanceClassService, DanceClassService>();
builder.Services.AddScoped<IAttendanceService, AttendanceService>();
builder.Services.AddScoped<IWorkshopService, WorkshopService>();
builder.Services.AddScoped<IWorkshopPricingService, WorkshopPricingService>();
builder.Services.AddScoped<IWorkshopTicketService, WorkshopTicketService>();
builder.Services.AddScoped<IStudentFeedbackService, StudentFeedbackService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IExternalNotificationSender, LoggingExternalNotificationSender>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IStudentNotificationReminderService, StudentNotificationReminderService>();
builder.Services.AddScoped<IStudentDashboardService, StudentDashboardService>();
builder.Services.AddScoped<IEchoAssistantService, EchoAssistantService>();

// Production Cloudflare R2 Media Services
builder.Services.Configure<CloudflareR2Settings>(
    builder.Configuration.GetSection("CloudflareR2"));
builder.Services.AddScoped<ICloudflareR2StorageService, CloudflareR2StorageService>();
builder.Services.AddScoped<IMediaService, MediaService>();

// Phase 2 & Phase 3 Trainer & Admin Services
builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
builder.Services.AddScoped<
    ITrainerApplicationVideoStorageService,
    LocalTrainerApplicationVideoStorageService>();
builder.Services.AddScoped<
    ITrainerVideoMetadataService,
    TrainerVideoMetadataService>();
builder.Services.AddScoped<ITrainerCodeService, TrainerCodeService>();
builder.Services.AddScoped<ITrainerPermissionService, TrainerPermissionService>();
builder.Services.AddScoped<ITrainerService, TrainerService>();
builder.Services.AddScoped<ITrainerApplicationService, TrainerApplicationService>();
builder.Services.AddScoped<ITrainerAvailabilityService, TrainerAvailabilityService>();
builder.Services.AddScoped<ITrainerTierService, TrainerTierService>();
builder.Services.AddScoped<ITrainerUpgradeService, TrainerUpgradeService>();
builder.Services.AddScoped<
    ITrainerProfilePhotoStorageService,
    LocalTrainerProfilePhotoStorageService>();
builder.Services.AddScoped<
    ITrainerGalleryStorageService,
    LocalTrainerGalleryStorageService>();
builder.Services.AddScoped<ITrainerGalleryService, TrainerGalleryService>();
builder.Services.AddScoped<ITrainerPerformanceService, TrainerPerformanceService>();
builder.Services.AddScoped<ITrainerWorkshopService, TrainerWorkshopService>();

// Phase 3 Admin Services
builder.Services.AddScoped<IAdminAuditService, AdminAuditService>();
builder.Services.AddScoped<IAdminDashboardService, AdminDashboardService>();
builder.Services.AddScoped<IAdminUserService, AdminUserService>();
builder.Services.AddScoped<IAdminStudentService, AdminStudentService>();
builder.Services.AddScoped<IAdminClassService, AdminClassService>();
builder.Services.AddScoped<IAdminPackageService, AdminPackageService>();
builder.Services.AddScoped<IAdminPaymentService, AdminPaymentService>();
builder.Services.AddScoped<IAdminFeedbackService, AdminFeedbackService>();
builder.Services.AddScoped<IGuestWorkshopFeedbackService, GuestWorkshopFeedbackService>();

builder.Services.AddScoped<IAdminTrainerService, AdminTrainerService>();
builder.Services.AddScoped<IAdminTrainerApplicationService, AdminTrainerApplicationService>();
builder.Services.AddScoped<IAdminTrainerTierService, AdminTrainerTierService>();
builder.Services.AddScoped<IAdminTrainerPermissionService, AdminTrainerPermissionService>();
builder.Services.AddScoped<IAdminTrainerUpgradeService, AdminTrainerUpgradeService>();
builder.Services.AddScoped<IAdminWorkshopService, AdminWorkshopService>();
builder.Services.AddScoped<IAdminAuthService, AdminAuthService>();
builder.Services.AddScoped<IAdminDeviceService, AdminDeviceService>();
builder.Services.AddScoped<IAdminAuthorizationService, AdminAuthorizationService>();

// Batch 3 Admin Services
builder.Services.AddScoped<IAdminBookingService, AdminBookingService>();
builder.Services.AddScoped<IAdminAttendanceService, AdminAttendanceService>();
builder.Services.AddScoped<IPaymentRefundService, PaymentRefundService>();
builder.Services.AddScoped<IPaymentReconciliationService, PaymentReconciliationService>();
builder.Services.AddScoped<IPaymentReceiptService, PaymentReceiptService>();
builder.Services.AddScoped<ITrainerPayoutService, TrainerPayoutService>();

// Batch 4 Admin Observability, Security Center & Incidents
builder.Services.AddSingleton<ITelemetryQueue, ChannelTelemetryQueue>();
builder.Services.AddHostedService<TelemetryBackgroundWorker>();
builder.Services.AddScoped<IAdminObservabilityService, AdminObservabilityService>();
builder.Services.AddScoped<IAdminSecurityCenterService, AdminSecurityCenterService>();
builder.Services.AddScoped<IAdminIncidentService, AdminIncidentService>();

// Batch 5 Corrective Actions & Communications
builder.Services.AddScoped<IAdminCommunicationsService, AdminCommunicationsService>();
builder.Services.AddScoped<IAdminCorrectiveActionService, AdminCorrectiveActionService>();

var jwtSettings = builder.Configuration
    .GetSection("Jwt")
    .Get<JwtSettings>()
    ?? throw new InvalidOperationException(
        "JWT configuration is missing.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,

            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),

            ValidateLifetime = true,

            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // Automatically apply any pending EF Core migrations to Neon
    if (db.Database.IsRelational())
    {
        db.Database.Migrate();
    }

    var passwordService = scope.ServiceProvider.GetRequiredService<IPasswordService>();
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

    Phase2SeedService
        .SeedAsync(db, passwordService, configuration, app.Environment.IsDevelopment())
        .GetAwaiter()
        .GetResult();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Must run before middleware that consumes RemoteIpAddress
// or Request.Scheme.
app.UseForwardedHeaders();
app.UseMiddleware<TraceCorrelationMiddleware>();
app.UseMiddleware<RequestTelemetryMiddleware>();

app.UseExceptionHandler(exceptionApp =>
{
    exceptionApp.Run(async context =>
    {
        var exceptionHandlerFeature =
            context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();

        var exception = exceptionHandlerFeature?.Error;

        // Log server-side with full context for production diagnostics.
        // The client response remains generic — no stack trace exposure.
        var logger = context.RequestServices
            .GetRequiredService<ILogger<Program>>();

        var traceId = context.TraceIdentifier;
        var method = context.Request.Method;
        var path = context.Request.Path;

        logger.LogError(
            exception,
            "Unhandled exception. TraceId: {TraceId} | {Method} {Path}",
            traceId, method, path);

        var statusCode =
            exception is UnauthorizedAccessException
                ? StatusCodes.Status403Forbidden
                : StatusCodes.Status500InternalServerError;

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        var problem = new
        {
            type = statusCode == StatusCodes.Status403Forbidden
                ? "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.4"
                : "https://www.rfc-editor.org/rfc/rfc9110#section-15.6.1",

            title = statusCode == StatusCodes.Status403Forbidden
                ? "Forbidden"
                : "An unexpected error occurred.",

            status = statusCode,

            detail = statusCode == StatusCodes.Status403Forbidden
                ? exception?.Message ?? "You are not authorized to perform this action."
                : "An unexpected error occurred while processing your request.",

            traceId
        };

        await context.Response.WriteAsJsonAsync(problem);
    });
});

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseMiddleware<SecurityHeadersMiddleware>();

app.UseCors();

var trainerPhotoPath = Path.Combine(
    app.Environment.ContentRootPath,
    "App_Data",
    "uploads",
    "trainer-profile-photos");

Directory.CreateDirectory(trainerPhotoPath);

app.UseStaticFiles(
    new StaticFileOptions
    {
        FileProvider =
            new PhysicalFileProvider(trainerPhotoPath),

        RequestPath =
            "/uploads/trainer-profile-photos"
    });

var studentPhotoPath = Path.Combine(
    app.Environment.ContentRootPath,
    "App_Data",
    "uploads",
    "student-profile-photos");

Directory.CreateDirectory(studentPhotoPath);

app.UseRouting();

app.UseRateLimiter();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();

