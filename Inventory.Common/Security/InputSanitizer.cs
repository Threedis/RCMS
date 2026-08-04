using System.Text.RegularExpressions;

namespace Inventory.Common.Security;

/// <summary>
/// Defence-in-depth helpers applied to free-text input before it is persisted.
/// <para>
/// SQL injection is already prevented structurally: every database call is a
/// parameterised stored-procedure invocation and no layer concatenates SQL.
/// Cross-site scripting is prevented by Razor's automatic HTML encoding.
/// These helpers exist as an additional barrier for values that are later
/// rendered into non-Razor surfaces such as Excel, PDF and e-mail bodies.
/// </para>
/// </summary>
public static class InputSanitizer
{
    private static readonly Regex ScriptBlock =
        new(@"<\s*script[^>]*>.*?<\s*/\s*script\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex HtmlTag =
        new(@"<[^>]+>", RegexOptions.Compiled);

    private static readonly Regex ControlChars =
        new(@"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]", RegexOptions.Compiled);

    /// <summary>
    /// Removes script blocks, HTML markup and control characters from user input.
    /// Returns an empty string for null/blank input.
    /// </summary>
    public static string StripMarkup(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var cleaned = ScriptBlock.Replace(value, string.Empty);
        cleaned = HtmlTag.Replace(cleaned, string.Empty);
        cleaned = ControlChars.Replace(cleaned, string.Empty);

        return cleaned.Trim();
    }

    /// <summary>
    /// Neutralises spreadsheet formula injection. Cells beginning with
    /// <c>=</c>, <c>+</c>, <c>-</c>, <c>@</c>, TAB or CR are prefixed with an
    /// apostrophe so Excel treats them as literal text.
    /// </summary>
    public static string SanitizeForSpreadsheet(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return "=+-@\t\r".Contains(value[0]) ? "'" + value : value;
    }

    /// <summary>
    /// Validates that a column name supplied by the client belongs to a
    /// white-list before it is handed to an <c>ORDER BY</c> inside a stored
    /// procedure. Returns <paramref name="fallback"/> when the value is unknown.
    /// </summary>
    public static string SafeSortColumn(string? requested, IReadOnlyCollection<string> allowed, string fallback)
    {
        if (string.IsNullOrWhiteSpace(requested))
        {
            return fallback;
        }

        var match = allowed.FirstOrDefault(c => string.Equals(c, requested, StringComparison.OrdinalIgnoreCase));
        return match ?? fallback;
    }
}
