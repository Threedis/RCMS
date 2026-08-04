namespace Inventory.Common.Configuration;

/// <summary>
/// Strongly typed binding of the <c>ApplicationSettings</c> section of
/// <c>appsettings.json</c>. Injected with <c>IOptions&lt;ApplicationSettings&gt;</c>.
/// </summary>
public sealed class ApplicationSettings
{
    public const string SectionName = "ApplicationSettings";

    /// <summary>Name shown in the navbar, page titles and report headers.</summary>
    public string ApplicationName { get; set; } = "Enterprise Inventory Management System";

    /// <summary>Company name printed on PDF report headers.</summary>
    public string CompanyName { get; set; } = "Threedis";

    /// <summary>Path, relative to the web root, of the logo used on reports.</summary>
    public string CompanyLogoPath { get; set; } = "images/logo.png";

    /// <summary>Sliding session lifetime, in minutes.</summary>
    public int SessionTimeoutMinutes { get; set; } = 30;

    /// <summary>Number of days a password stays valid.</summary>
    public int PasswordExpiryDays { get; set; } = 90;

    /// <summary>Rows returned by default on every grid.</summary>
    public int DefaultPageSize { get; set; } = 25;

    /// <summary>Enables the in-app notification poller.</summary>
    public bool EnableInAppNotifications { get; set; } = true;

    /// <summary>Enables outbound e-mail. Turn off in non-production environments.</summary>
    public bool EnableEmailNotifications { get; set; }

    /// <summary>Days of inactivity after which stock is treated as dead.</summary>
    public int DeadStockDays { get; set; } = 180;

    /// <summary>Issues per period above which an item is "fast moving".</summary>
    public int FastMovingThreshold { get; set; } = 12;
}

/// <summary>SMTP configuration for the notification e-mail sender.</summary>
public sealed class EmailSettings
{
    public const string SectionName = "EmailSettings";

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 587;

    public bool UseSsl { get; set; } = true;

    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// Never commit a real password. Supply it through User Secrets in
    /// development and an environment variable / Key Vault in production.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    public string FromAddress { get; set; } = "no-reply@example.com";

    public string FromDisplayName { get; set; } = "Inventory Management System";

    /// <summary>When set, every outgoing mail is redirected here (safe testing).</summary>
    public string? OverrideRecipient { get; set; }
}

/// <summary>File upload / attachment storage configuration.</summary>
public sealed class FileStorageSettings
{
    public const string SectionName = "FileStorageSettings";

    /// <summary>
    /// Physical or web-root-relative folder that receives uploads.
    /// Must be outside the application's static file pipeline in production.
    /// </summary>
    public string RootPath { get; set; } = "App_Data/Uploads";

    /// <summary>Maximum accepted size, in megabytes.</summary>
    public int MaxFileSizeMb { get; set; } = 10;

    /// <summary>Extensions accepted by the upload endpoint (lower case, with dot).</summary>
    public string[] AllowedExtensions { get; set; } =
    {
        ".pdf", ".docx", ".xlsx", ".jpg", ".jpeg", ".png"
    };
}
