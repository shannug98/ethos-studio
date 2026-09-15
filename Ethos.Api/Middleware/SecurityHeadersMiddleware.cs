namespace Ethos.Api.Middleware;

public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            // Prevent MIME-type sniffing.
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";

            // Prevent clickjacking.
            context.Response.Headers["X-Frame-Options"] = "DENY";

            // Limit referrer information sent by the browser.
            context.Response.Headers["Referrer-Policy"] =
                "strict-origin-when-cross-origin";

            return Task.CompletedTask;
        });

        await _next(context);
    }
}
