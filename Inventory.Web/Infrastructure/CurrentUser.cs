using System.Security.Claims;
using Inventory.Common.Constants;
using Inventory.Common.Security;

namespace Inventory.Web.Infrastructure;

/// <summary>
/// Reads the signed-in principal from the current HTTP request.
/// <para>
/// This is the only place in the solution that touches
/// <see cref="IHttpContextAccessor"/>. Every other layer depends on
/// <see cref="ICurrentUser"/>, which is why they stay unit testable without a
/// web host.
/// </para>
/// </summary>
public sealed class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentUser(IHttpContextAccessor accessor)
    {
        _accessor = accessor ?? throw new ArgumentNullException(nameof(accessor));
    }

    private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    /// <inheritdoc />
    public int UserId
    {
        get
        {
            var value = Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(value, out var id) ? id : 0;
        }
    }

    /// <inheritdoc />
    public string UserName => Principal?.Identity?.Name ?? "SYSTEM";

    /// <inheritdoc />
    public string FullName =>
        Principal?.FindFirstValue(AppClaimTypes.FullName)
        ?? Principal?.Identity?.Name
        ?? "System";

    /// <inheritdoc />
    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    /// <inheritdoc />
    public IReadOnlyCollection<string> Roles =>
        Principal?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray() ?? Array.Empty<string>();

    /// <inheritdoc />
    public string? IpAddress
    {
        get
        {
            var context = _accessor.HttpContext;

            if (context is null)
            {
                return null;
            }

            // Behind a reverse proxy or load balancer the real client address
            // arrives in X-Forwarded-For; the first entry is the originating IP.
            if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwarded)
                && !string.IsNullOrWhiteSpace(forwarded))
            {
                var first = forwarded.ToString().Split(',')[0].Trim();

                if (!string.IsNullOrWhiteSpace(first))
                {
                    return first.Length > 45 ? first[..45] : first;
                }
            }

            return context.Connection.RemoteIpAddress?.ToString();
        }
    }

    /// <inheritdoc />
    public string? UserAgent
    {
        get
        {
            var agent = _accessor.HttpContext?.Request.Headers.UserAgent.ToString();
            return string.IsNullOrWhiteSpace(agent) ? null : agent.Length > 400 ? agent[..400] : agent;
        }
    }

    /// <inheritdoc />
    public bool IsInRole(string role) => Principal?.IsInRole(role) ?? false;
}
