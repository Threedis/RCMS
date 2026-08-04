namespace Inventory.Common.Models;

/// <summary>
/// Standard outcome envelope returned by every business service method.
/// Business failures are values, not exceptions — exceptions are reserved for
/// genuinely exceptional conditions (infrastructure faults, bugs).
/// </summary>
public class ServiceResult
{
    /// <summary>True when the operation completed successfully.</summary>
    public bool Succeeded { get; protected init; }

    /// <summary>Human readable outcome message, safe to show to end users.</summary>
    public string Message { get; protected init; } = string.Empty;

    /// <summary>Field level validation messages keyed by property name.</summary>
    public IDictionary<string, string[]> Errors { get; protected init; }
        = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Return code propagated from the stored procedure (0 = success).
    /// Useful for callers that need to branch on a specific business rule.
    /// </summary>
    public int Code { get; protected init; }

    public static ServiceResult Success(string message = "Operation completed successfully.")
        => new() { Succeeded = true, Message = message, Code = 0 };

    public static ServiceResult Failure(string message, int code = -1)
        => new() { Succeeded = false, Message = message, Code = code };

    public static ServiceResult Failure(string message, IDictionary<string, string[]> errors, int code = -1)
        => new() { Succeeded = false, Message = message, Errors = errors, Code = code };

    /// <summary>Builds a failure from a flat list of validation messages.</summary>
    public static ServiceResult ValidationFailure(IEnumerable<string> messages)
    {
        var list = messages.ToArray();
        return new ServiceResult
        {
            Succeeded = false,
            Code = -400,
            Message = list.Length == 1 ? list[0] : "One or more validation errors occurred.",
            Errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                [string.Empty] = list
            }
        };
    }
}

/// <summary>Outcome envelope carrying a payload.</summary>
/// <typeparam name="T">Type of the returned data.</typeparam>
public sealed class ServiceResult<T> : ServiceResult
{
    /// <summary>The payload; <c>default</c> when the operation failed.</summary>
    public T? Data { get; private init; }

    public static ServiceResult<T> Success(T data, string message = "Operation completed successfully.")
        => new() { Succeeded = true, Message = message, Data = data, Code = 0 };

    public static new ServiceResult<T> Failure(string message, int code = -1)
        => new() { Succeeded = false, Message = message, Code = code };

    public static new ServiceResult<T> Failure(string message, IDictionary<string, string[]> errors, int code = -1)
        => new() { Succeeded = false, Message = message, Errors = errors, Code = code };

    /// <summary>Converts a non-generic failure into a typed failure.</summary>
    public static ServiceResult<T> From(ServiceResult result)
        => new() { Succeeded = result.Succeeded, Message = result.Message, Errors = result.Errors, Code = result.Code };
}
