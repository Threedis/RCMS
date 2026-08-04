namespace Inventory.Common.Exceptions;

/// <summary>Base type for every exception raised deliberately by the application.</summary>
public abstract class InventoryException : Exception
{
    protected InventoryException(string message)
        : base(message)
    {
    }

    protected InventoryException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Return code surfaced to the caller / stored-procedure contract.</summary>
    public abstract int Code { get; }
}

/// <summary>A business rule was violated (for example: issuing more than the stock on hand).</summary>
public sealed class BusinessRuleException : InventoryException
{
    public BusinessRuleException(string message)
        : base(message)
    {
    }

    public BusinessRuleException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public override int Code => -409;
}

/// <summary>A requested entity does not exist.</summary>
public sealed class EntityNotFoundException : InventoryException
{
    public EntityNotFoundException(string entityName, object key)
        : base($"{entityName} with identifier '{key}' was not found.")
    {
        EntityName = entityName;
        Key = key;
    }

    public string EntityName { get; }

    public object Key { get; }

    public override int Code => -404;
}

/// <summary>The current user is not permitted to perform the requested operation.</summary>
public sealed class ForbiddenOperationException : InventoryException
{
    public ForbiddenOperationException(string message)
        : base(message)
    {
    }

    public override int Code => -403;
}

/// <summary>Input failed validation before it reached the database.</summary>
public sealed class ValidationException : InventoryException
{
    public ValidationException(string message)
        : base(message)
    {
        Errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
    }

    public ValidationException(string message, IDictionary<string, string[]> errors)
        : base(message)
    {
        Errors = errors;
    }

    public IDictionary<string, string[]> Errors { get; }

    public override int Code => -400;
}

/// <summary>Raised when a stored procedure reports a non-zero return code.</summary>
public sealed class DataAccessException : InventoryException
{
    public DataAccessException(string procedureName, int returnCode, string message)
        : base($"Stored procedure '{procedureName}' failed ({returnCode}): {message}")
    {
        ProcedureName = procedureName;
        ReturnCode = returnCode;
    }

    public DataAccessException(string procedureName, string message, Exception innerException)
        : base($"Stored procedure '{procedureName}' failed: {message}", innerException)
    {
        ProcedureName = procedureName;
        ReturnCode = -500;
    }

    public string ProcedureName { get; }

    public int ReturnCode { get; }

    public override int Code => ReturnCode;
}
