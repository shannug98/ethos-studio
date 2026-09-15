using System.Text.RegularExpressions;

namespace Ethos.Api.Middleware;

public sealed partial class TraceCorrelationMiddleware
{
    private readonly RequestDelegate _next;
    private static readonly Regex SafeCorrelationRegex = new(@"^[a-zA-Z0-9_\-]{1,64}$", RegexOptions.Compiled);

    public TraceCorrelationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // 1. Authoritative Server-Generated Trace ID (Never trust client to define audit trace ID)
        var traceId = $"trc_{Guid.NewGuid():N}";
        context.Items["TraceId"] = traceId;

        // 2. Client-supplied correlation ID: preserved for client reference only if valid
        string? correlationId = null;
        if (context.Request.Headers.TryGetValue("X-Correlation-ID", out var clientCorr) ||
            context.Request.Headers.TryGetValue("X-Correlation-Id", out clientCorr))
        {
            var raw = clientCorr.ToString().Trim();
            if (!string.IsNullOrEmpty(raw) && SafeCorrelationRegex.IsMatch(raw))
            {
                correlationId = raw;
                context.Items["CorrelationId"] = correlationId;
            }
        }

        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-Trace-Id"] = traceId;
            if (!string.IsNullOrEmpty(correlationId))
            {
                context.Response.Headers["X-Correlation-Id"] = correlationId;
            }
            return Task.CompletedTask;
        });

        await _next(context);
    }
}

public static class TraceCorrelationExtensions
{
    public static string GetTraceId(this HttpContext? context)
    {
        if (context?.Items.TryGetValue("TraceId", out var t) == true && t is string traceId)
        {
            return traceId;
        }
        return $"trc_{Guid.NewGuid():N}";
    }

    public static string? GetCorrelationId(this HttpContext? context)
    {
        if (context?.Items.TryGetValue("CorrelationId", out var c) == true && c is string corrId)
        {
            return corrId;
        }
        return null;
    }
}
