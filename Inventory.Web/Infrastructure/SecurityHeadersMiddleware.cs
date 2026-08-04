namespace Inventory.Web.Infrastructure;

/// <summary>
/// Adds the response headers that harden the application in the browser.
/// <para>
/// The Content Security Policy is the important one: it stops an injected
/// script from executing even if a cross-site scripting hole ever slipped
/// through Razor's automatic encoding. All front-end libraries are served from
/// <c>wwwroot</c>, so no external script or style origin is allowed.
/// </para>
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        // Never let the browser sniff a response into a different content type.
        headers["X-Content-Type-Options"] = "nosniff";

        // Legacy click-jacking protection for browsers without frame-ancestors.
        headers["X-Frame-Options"] = "DENY";

        // Send the origin only, so query strings never leak to other sites.
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        // Nothing in this application uses these device APIs.
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";

        headers["X-XSS-Protection"] = "0";  // superseded by CSP; the legacy filter caused its own bugs

        headers["Content-Security-Policy"] = string.Join("; ",
            "default-src 'self'",
            // 'unsafe-inline' is required for the small inline chart bootstraps
            // in the dashboard view; everything else is a served file.
            "script-src 'self' 'unsafe-inline'",
            "style-src 'self' 'unsafe-inline'",
            "img-src 'self' data: blob:",
            "font-src 'self' data:",
            "connect-src 'self'",
            "frame-ancestors 'none'",
            "form-action 'self'",
            "base-uri 'self'",
            "object-src 'none'",
            "upgrade-insecure-requests");

        // Do not let a browser or proxy cache an authenticated page.
        if (context.User.Identity?.IsAuthenticated == true)
        {
            headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
            headers["Pragma"] = "no-cache";
        }

        // The server banner tells an attacker what to target; remove it.
        headers.Remove("Server");
        headers.Remove("X-Powered-By");

        await _next(context);
    }
}
