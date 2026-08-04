namespace Inventory.DAL.StoredProcedures;

/// <summary>
/// Outcome of a write stored procedure.
/// <para>
/// Every write procedure in <c>Inventory.Database</c> follows the same contract:
/// it declares <c>@ReturnCode INT OUTPUT</c>, <c>@Message NVARCHAR(500) OUTPUT</c>
/// and, for inserts, <c>@NewId INT OUTPUT</c>. A return code of zero means the
/// transaction committed; a negative code is a business rule rejection with a
/// user-safe message; anything else is an unhandled SQL error which the
/// procedure has already logged and rolled back.
/// </para>
/// </summary>
public sealed class SpResult
{
    /// <summary>Zero on success, negative for a handled business failure.</summary>
    public int ReturnCode { get; init; }

    /// <summary>User-safe description of the outcome.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Identity of the inserted row, when the procedure produced one.</summary>
    public int? NewId { get; init; }

    /// <summary>Generated business number (GRN / issue / item code), when applicable.</summary>
    public string? GeneratedNumber { get; init; }

    public bool IsSuccess => ReturnCode == 0;

    public static SpResult Success(int? newId = null, string? number = null, string message = "Saved successfully.")
        => new() { ReturnCode = 0, NewId = newId, GeneratedNumber = number, Message = message };

    public static SpResult Failure(string message, int code = -1)
        => new() { ReturnCode = code, Message = message };
}

/// <summary>Names of the output parameters shared by every write procedure.</summary>
public static class SpParameters
{
    public const string ReturnCode = "@ReturnCode";
    public const string Message = "@Message";
    public const string NewId = "@NewId";
    public const string GeneratedNumber = "@GeneratedNumber";
    public const string TotalCount = "@TotalCount";
    public const string CurrentUserId = "@CurrentUserId";
}
