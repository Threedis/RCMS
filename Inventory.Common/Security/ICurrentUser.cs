namespace Inventory.Common.Security;

/// <summary>
/// Ambient information about the user executing the current request.
/// Implemented in the web layer over <c>IHttpContextAccessor</c>; business and
/// data layers depend only on this abstraction, which keeps them unit testable.
/// </summary>
public interface ICurrentUser
{
    /// <summary>Primary key of the signed-in user, or 0 for background/system work.</summary>
    int UserId { get; }

    /// <summary>Login name; <c>SYSTEM</c> when no user is attached to the request.</summary>
    string UserName { get; }

    /// <summary>Display name used on audit rows and approval history.</summary>
    string FullName { get; }

    /// <summary>True when an authenticated principal is present.</summary>
    bool IsAuthenticated { get; }

    /// <summary>Roles assigned to the current principal.</summary>
    IReadOnlyCollection<string> Roles { get; }

    /// <summary>Client IP address, recorded on audit and login history rows.</summary>
    string? IpAddress { get; }

    /// <summary>User agent string, recorded on login history rows.</summary>
    string? UserAgent { get; }

    /// <summary>True when the principal holds <paramref name="role"/>.</summary>
    bool IsInRole(string role);
}

/// <summary>
/// Null-object implementation used by scheduled jobs, seeders and unit tests,
/// where there is no HTTP request in flight.
/// </summary>
public sealed class SystemUser : ICurrentUser
{
    public static readonly SystemUser Instance = new();

    public int UserId => 0;

    public string UserName => "SYSTEM";

    public string FullName => "System";

    public bool IsAuthenticated => false;

    public IReadOnlyCollection<string> Roles => Array.Empty<string>();

    public string? IpAddress => null;

    public string? UserAgent => null;

    public bool IsInRole(string role) => false;
}
