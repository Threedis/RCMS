using Inventory.Common.Configuration;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Inventory.Services.Email;

/// <summary>An outbound message.</summary>
public sealed class EmailMessage
{
    public required IReadOnlyList<string> To { get; init; }

    public IReadOnlyList<string> Cc { get; init; } = Array.Empty<string>();

    public required string Subject { get; init; }

    /// <summary>HTML body. Plain text is derived automatically.</summary>
    public required string HtmlBody { get; init; }
}

/// <summary>Sends notification e-mail. Failures are logged, never thrown at the caller.</summary>
public interface IEmailSender
{
    /// <summary>Sends one message. Returns false when sending was skipped or failed.</summary>
    Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken = default);

    /// <summary>Wraps <paramref name="bodyHtml"/> in the standard branded template.</summary>
    string BuildTemplate(string title, string bodyHtml, string? actionUrl = null, string? actionText = null);
}

/// <summary>MailKit-backed <see cref="IEmailSender"/>.</summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly EmailSettings _emailSettings;
    private readonly ApplicationSettings _appSettings;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(
        IOptions<EmailSettings> emailSettings,
        IOptions<ApplicationSettings> appSettings,
        ILogger<SmtpEmailSender> logger)
    {
        _emailSettings = emailSettings?.Value ?? new EmailSettings();
        _appSettings = appSettings?.Value ?? new ApplicationSettings();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (!_appSettings.EnableEmailNotifications)
        {
            _logger.LogDebug("E-mail disabled by configuration; '{Subject}' was not sent.", message.Subject);
            return false;
        }

        if (string.IsNullOrWhiteSpace(_emailSettings.Host))
        {
            _logger.LogWarning("No SMTP host configured; '{Subject}' was not sent.", message.Subject);
            return false;
        }

        // A configured override keeps non-production environments from mailing
        // real users while still exercising the whole path.
        var recipients = string.IsNullOrWhiteSpace(_emailSettings.OverrideRecipient)
            ? message.To
            : new[] { _emailSettings.OverrideRecipient! };

        var valid = recipients.Where(IsValidAddress).ToList();

        if (valid.Count == 0)
        {
            _logger.LogWarning("No valid recipient for '{Subject}'.", message.Subject);
            return false;
        }

        try
        {
            var mime = new MimeMessage();
            mime.From.Add(new MailboxAddress(_emailSettings.FromDisplayName, _emailSettings.FromAddress));

            foreach (var address in valid)
            {
                mime.To.Add(MailboxAddress.Parse(address));
            }

            if (string.IsNullOrWhiteSpace(_emailSettings.OverrideRecipient))
            {
                foreach (var address in message.Cc.Where(IsValidAddress))
                {
                    mime.Cc.Add(MailboxAddress.Parse(address));
                }
            }

            mime.Subject = message.Subject;

            var builder = new BodyBuilder
            {
                HtmlBody = message.HtmlBody,
                TextBody = StripHtml(message.HtmlBody)
            };

            mime.Body = builder.ToMessageBody();

            using var client = new SmtpClient();

            await client.ConnectAsync(
                _emailSettings.Host,
                _emailSettings.Port,
                _emailSettings.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto,
                cancellationToken).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(_emailSettings.UserName))
            {
                await client.AuthenticateAsync(_emailSettings.UserName, _emailSettings.Password, cancellationToken)
                    .ConfigureAwait(false);
            }

            await client.SendAsync(mime, cancellationToken).ConfigureAwait(false);
            await client.DisconnectAsync(true, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Sent '{Subject}' to {Count} recipient(s).", message.Subject, valid.Count);
            return true;
        }
        catch (Exception ex)
        {
            // A mail outage must never fail the business transaction that
            // triggered the notification.
            _logger.LogError(ex, "Failed to send '{Subject}'.", message.Subject);
            return false;
        }
    }

    /// <inheritdoc />
    public string BuildTemplate(string title, string bodyHtml, string? actionUrl = null, string? actionText = null)
    {
        var button = string.IsNullOrWhiteSpace(actionUrl)
            ? string.Empty
            : $"""
               <tr><td style="padding:18px 24px 0 24px;">
                 <a href="{System.Net.WebUtility.HtmlEncode(actionUrl)}"
                    style="background:#0d6efd;color:#ffffff;text-decoration:none;padding:10px 20px;
                           border-radius:4px;display:inline-block;font-weight:600;">
                   {System.Net.WebUtility.HtmlEncode(actionText ?? "Open")}
                 </a>
               </td></tr>
               """;

        return $"""
                <!DOCTYPE html>
                <html><body style="margin:0;padding:0;background:#f4f6f9;font-family:Segoe UI,Arial,sans-serif;">
                  <table width="100%" cellpadding="0" cellspacing="0" style="background:#f4f6f9;padding:24px 0;">
                    <tr><td align="center">
                      <table width="600" cellpadding="0" cellspacing="0"
                             style="background:#ffffff;border-radius:6px;overflow:hidden;
                                    box-shadow:0 1px 3px rgba(0,0,0,.12);">
                        <tr><td style="background:#0d6efd;color:#ffffff;padding:16px 24px;font-size:18px;font-weight:600;">
                          {System.Net.WebUtility.HtmlEncode(_appSettings.ApplicationName)}
                        </td></tr>
                        <tr><td style="padding:24px 24px 0 24px;font-size:16px;font-weight:600;color:#212529;">
                          {System.Net.WebUtility.HtmlEncode(title)}
                        </td></tr>
                        <tr><td style="padding:12px 24px;font-size:14px;color:#495057;line-height:1.6;">
                          {bodyHtml}
                        </td></tr>
                        {button}
                        <tr><td style="padding:24px;font-size:11px;color:#adb5bd;border-top:1px solid #e9ecef;">
                          This is an automated message from {System.Net.WebUtility.HtmlEncode(_appSettings.CompanyName)}.
                          Please do not reply.
                        </td></tr>
                      </table>
                    </td></tr>
                  </table>
                </body></html>
                """;
    }

    private static bool IsValidAddress(string? address)
        => !string.IsNullOrWhiteSpace(address) && MailboxAddress.TryParse(address, out _);

    private static string StripHtml(string html)
        => System.Net.WebUtility.HtmlDecode(
            System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " "))
            .Replace("  ", " ", StringComparison.Ordinal)
            .Trim();
}
