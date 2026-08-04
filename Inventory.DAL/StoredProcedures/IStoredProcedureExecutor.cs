using System.Data;

namespace Inventory.DAL.StoredProcedures;

/// <summary>
/// The single gateway between managed code and SQL Server.
/// <para>
/// Every call is a parameterised stored-procedure invocation
/// (<see cref="CommandType.StoredProcedure"/>). No implementation in this
/// solution ever concatenates a SQL string, which makes SQL injection
/// structurally impossible rather than merely unlikely.
/// </para>
/// <para>
/// The executor shares the <see cref="ApplicationDbContext"/>'s connection and
/// enlists in its ambient transaction, so stored-procedure writes and EF writes
/// inside one unit of work commit or roll back together.
/// </para>
/// </summary>
public interface IStoredProcedureExecutor
{
    /// <summary>Executes a procedure and materialises the first result set.</summary>
    Task<IReadOnlyList<T>> QueryAsync<T>(
        string procedureName,
        object? parameters = null,
        CancellationToken cancellationToken = default);

    /// <summary>Executes a procedure and returns the first row, or default.</summary>
    Task<T?> QuerySingleOrDefaultAsync<T>(
        string procedureName,
        object? parameters = null,
        CancellationToken cancellationToken = default);

    /// <summary>Executes a procedure and returns the first column of the first row.</summary>
    Task<T?> ExecuteScalarAsync<T>(
        string procedureName,
        object? parameters = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a paged query procedure. The procedure must return the page of
    /// rows as its result set and the unfiltered row count through the
    /// <c>@TotalCount</c> output parameter.
    /// </summary>
    Task<(IReadOnlyList<T> Items, int TotalCount)> QueryPagedAsync<T>(
        string procedureName,
        object? parameters = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a write procedure and reads the standard
    /// <c>@ReturnCode</c> / <c>@Message</c> / <c>@NewId</c> output contract.
    /// </summary>
    Task<SpResult> ExecuteAsync(
        string procedureName,
        object? parameters = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a procedure that returns several result sets and projects them
    /// through <paramref name="reader"/>. Used by the dashboard and by the
    /// document-with-lines loaders so one round trip fills a whole screen.
    /// </summary>
    Task<TResult> QueryMultipleAsync<TResult>(
        string procedureName,
        Func<IMultipleResultReader, Task<TResult>> reader,
        object? parameters = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Sequential reader over the result sets of a multi-result stored procedure.
/// Abstracted so no layer above the DAL takes a dependency on the micro-ORM.
/// </summary>
public interface IMultipleResultReader
{
    /// <summary>Reads the next result set as a list.</summary>
    Task<IReadOnlyList<T>> ReadAsync<T>();

    /// <summary>Reads the next result set expecting a single row (or none).</summary>
    Task<T?> ReadSingleOrDefaultAsync<T>();
}
