using System.Diagnostics;
using System.Security.Claims;
using Ethos.Api.Application.Common;
using Ethos.Api.Domain.Entities;

namespace Ethos.Api.Middleware;

public class RequestTelemetryMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ITelemetryQueue _queue;

    public RequestTelemetryMiddleware(RequestDelegate next, ITelemetryQueue queue)
    {
        _next = next;
        _queue = queue;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            await _next(context);
        }
        finally
        {
            sw.Stop();

            try
            {
                // Capture telemetry asynchronously without blocking client
                var traceId = context.GetTraceId();
                var correlationId = context.GetCorrelationId();
                var method = context.Request.Method;
                var path = context.Request.Path.Value ?? "/";

                // Ignore swagger / static assets to avoid flooding
                if (!path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase) &&
                    !path.StartsWith("/favicon", StringComparison.OrdinalIgnoreCase))
                {
                    var statusCode = context.Response.StatusCode;
                    var ip = context.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
                    var userAgent = context.Request.Headers.UserAgent.ToString();
                    if (userAgent.Length > 500) userAgent = userAgent[..500];

                    Guid? userId = null;
                    string? role = null;

                    var user = context.User;
                    if (user?.Identity?.IsAuthenticated == true)
                    {
                        var sub = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                               ?? user.FindFirst("sub")?.Value
                               ?? user.FindFirst("userId")?.Value;

                        if (Guid.TryParse(sub, out var parsedId))
                        {
                            userId = parsedId;
                        }

                        role = user.FindFirst(ClaimTypes.Role)?.Value
                            ?? user.FindFirst("role")?.Value;
                    }

                    string? errorMsg = null;
                    if (statusCode >= 400)
                    {
                        errorMsg = $"HTTP {statusCode} {path}";
                    }

                    var log = new ApiRequestLog
                    {
                        Id = Guid.NewGuid(),
                        TraceId = traceId,
                        CorrelationId = correlationId,
                        Method = method,
                        Path = path.Length > 500 ? path[..500] : path,
                        StatusCode = statusCode,
                        DurationMs = sw.ElapsedMilliseconds,
                        IpAddress = ip.Length > 100 ? ip[..100] : ip,
                        UserAgent = string.IsNullOrEmpty(userAgent) ? null : userAgent,
                        UserId = userId,
                        Role = role,
                        ErrorMessage = errorMsg,
                        CreatedAt = DateTime.UtcNow
                    };

                    _queue.Enqueue(log);
                }
            }
            catch
            {
                // Telemetry failures must NEVER affect HTTP pipeline
            }
        }
    }
}