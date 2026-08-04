using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Inventory.Common.Constants;
using Inventory.Common.Enums;

namespace Inventory.Common.Extensions;

/// <summary>Small, dependency-free helpers shared by every layer.</summary>
public static class StringExtensions
{
    private static readonly Regex UnsafeFileChars = new(@"[^A-Za-z0-9._\- ]", RegexOptions.Compiled);
    private static readonly Regex MultipleSpaces = new(@"\s{2,}", RegexOptions.Compiled);

    /// <summary>True when the value is null, empty or only white space.</summary>
    public static bool IsBlank(this string? value) => string.IsNullOrWhiteSpace(value);

    /// <summary>Trims and collapses inner whitespace; returns null for blank input.</summary>
    public static string? NormalizeOrNull(this string? value)
        => value.IsBlank() ? null : MultipleSpaces.Replace(value!.Trim(), " ");

    /// <summary>Trims and collapses inner whitespace; returns empty string for blank input.</summary>
    public static string NormalizeOrEmpty(this string? value)
        => value.NormalizeOrNull() ?? string.Empty;

    /// <summary>Truncates to <paramref name="maxLength"/> without throwing on short input.</summary>
    public static string Truncate(this string? value, int maxLength)
    {
        if (value.IsBlank())
        {
            return string.Empty;
        }

        return value!.Length <= maxLength ? value : value[..maxLength];
    }

    /// <summary>
    /// Strips characters that are unsafe in a file name and collapses the result.
    /// Used before any user supplied name touches the file system.
    /// </summary>
    public static string ToSafeFileName(this string? value)
    {
        var cleaned = UnsafeFileChars.Replace(value.NormalizeOrEmpty(), "_");
        return cleaned.IsBlank() ? "file" : cleaned.Truncate(120);
    }

    /// <summary>Escapes the four characters that matter when writing to CSV.</summary>
    public static string ToCsvField(this string? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        var needsQuotes = value.Contains(',', StringComparison.Ordinal)
                          || value.Contains('"', StringComparison.Ordinal)
                          || value.Contains('\n', StringComparison.Ordinal)
                          || value.Contains('\r', StringComparison.Ordinal);

        // A leading =, +, - or @ turns a cell into a formula in Excel; prefix a
        // single quote so exported data can never be executed (CSV injection).
        var safe = value.Length > 0 && "=+-@\t\r".Contains(value[0]) ? "'" + value : value;

        return needsQuotes ? "\"" + safe.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"" : safe;
    }

    /// <summary>Masks all but the last four characters, for logging identifiers.</summary>
    public static string Mask(this string? value, int visibleSuffix = 4)
    {
        if (value.IsBlank())
        {
            return string.Empty;
        }

        return value!.Length <= visibleSuffix
            ? new string('*', value.Length)
            : new string('*', value.Length - visibleSuffix) + value[^visibleSuffix..];
    }

    /// <summary>Case-insensitive contains, used by in-memory filters.</summary>
    public static bool ContainsIgnoreCase(this string? source, string? term)
        => !source.IsBlank() && !term.IsBlank()
           && source!.Contains(term!, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Formatting helpers that guarantee one consistent presentation everywhere.</summary>
public static class FormattingExtensions
{
    private static readonly CultureInfo AppCulture = CultureInfo.GetCultureInfo(AppConstants.DefaultCulture);

    public static string ToDisplayDate(this DateTime value)
        => value.ToString(AppConstants.DateFormat, AppCulture);

    public static string ToDisplayDate(this DateTime? value)
        => value?.ToDisplayDate() ?? string.Empty;

    public static string ToDisplayDateTime(this DateTime value)
        => value.ToString(AppConstants.DateTimeFormat, AppCulture);

    public static string ToDisplayDateTime(this DateTime? value)
        => value?.ToDisplayDateTime() ?? string.Empty;

    public static string ToMoney(this decimal value)
        => AppConstants.CurrencySymbol + value.ToString("N2", AppCulture);

    public static string ToMoney(this decimal? value)
        => (value ?? 0m).ToMoney();

    public static string ToQuantity(this decimal value)
        => value.ToString("N3", AppCulture).TrimEnd('0').TrimEnd('.');
}

/// <summary>Date helpers used by the period-based consumption reports.</summary>
public static class DateExtensions
{
    public static DateTime StartOfDay(this DateTime value) => value.Date;

    public static DateTime EndOfDay(this DateTime value) => value.Date.AddDays(1).AddTicks(-1);

    public static DateTime StartOfMonth(this DateTime value) => new(value.Year, value.Month, 1);

    public static DateTime EndOfMonth(this DateTime value)
        => value.StartOfMonth().AddMonths(1).AddTicks(-1);

    /// <summary>Calendar quarter (1-4) the date falls in.</summary>
    public static int Quarter(this DateTime value) => ((value.Month - 1) / 3) + 1;

    /// <summary>Calendar half (1-2) the date falls in.</summary>
    public static int Half(this DateTime value) => value.Month <= 6 ? 1 : 2;

    /// <summary>
    /// Indian financial year label for the date, e.g. <c>2025-26</c>.
    /// The financial year starts on 1 April.
    /// </summary>
    public static string FinancialYear(this DateTime value)
    {
        var startYear = value.Month >= 4 ? value.Year : value.Year - 1;
        return $"{startYear}-{(startYear + 1) % 100:00}";
    }
}

/// <summary>Enumerable helpers.</summary>
public static class EnumerableExtensions
{
    /// <summary>Never-null enumeration.</summary>
    public static IEnumerable<T> OrEmpty<T>(this IEnumerable<T>? source) => source ?? Array.Empty<T>();

    /// <summary>Splits a sequence into fixed size batches (used by bulk import).</summary>
    public static IEnumerable<IReadOnlyList<T>> Batch<T>(this IEnumerable<T> source, int size)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfLessThan(size, 1);

        var bucket = new List<T>(size);
        foreach (var item in source)
        {
            bucket.Add(item);
            if (bucket.Count != size)
            {
                continue;
            }

            yield return bucket;
            bucket = new List<T>(size);
        }

        if (bucket.Count > 0)
        {
            yield return bucket;
        }
    }

    /// <summary>Joins values into a single display string.</summary>
    public static string JoinWith<T>(this IEnumerable<T>? source, string separator = ", ")
        => string.Join(separator, source.OrEmpty().Select(x => x?.ToString()).Where(x => !x.IsBlank()));
}

/// <summary>Enum helpers used for drop-downs and display labels.</summary>
public static class EnumExtensions
{
    /// <summary>Splits a PascalCase enum name into words: <c>PendingApproval</c> to <c>Pending Approval</c>.</summary>
    public static string ToDisplayName(this Enum value)
    {
        var name = value.ToString();
        var builder = new StringBuilder(name.Length + 4);

        for (var i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]) && !char.IsUpper(name[i - 1]))
            {
                builder.Append(' ');
            }

            builder.Append(name[i]);
        }

        return builder.ToString();
    }

    /// <summary>Bootstrap contextual class used to colour status badges.</summary>
    public static string ToBadgeClass(this DocumentStatus status) => status switch
    {
        DocumentStatus.Draft => "secondary",
        DocumentStatus.PendingApproval => "warning",
        DocumentStatus.Approved => "success",
        DocumentStatus.Rejected => "danger",
        DocumentStatus.Issued => "info",
        DocumentStatus.Closed => "dark",
        DocumentStatus.Cancelled => "danger",
        _ => "secondary"
    };
}
