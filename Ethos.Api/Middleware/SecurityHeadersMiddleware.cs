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
        // Prevent MIME-type sniffing.
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";

        // Prevent clickjacking.
        context.Response.Headers["X-Frame-Options"] = "DENY";

        // Limit referrer information sent by the browser.
        context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        // Enforce restrictive Content-Security-Policy
        context.Response.Headers["Content-Security-Policy"] =
            "default-src 'self'; " +
            "script-src 'self' https://checkout.razorpay.com; " +
            "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
            "font-src 'self' https://fonts.gstatic.com data:; " +
            "img-src 'self' data: blob: https://media.ethosdancestudio.com https://*.r2.cloudflarestorage.com https://maps.gstatic.com https://*.googleapis.com; " +
            "media-src 'self' blob: https://media.ethosdancestudio.com https://*.r2.cloudflarestorage.com; " +
            "connect-src 'self' https://api.razorpay.com https://lumberjack.razorpay.com https://media.ethosdancestudio.com https://maps.googleapis.com; " +
            "frame-src 'self' https://api.razorpay.com https://checkout.razorpay.com; " +
            "object-src 'none'; " +
            "base-uri 'self'; " +
            "form-action 'self';";

        await _next(context);
    }
}
