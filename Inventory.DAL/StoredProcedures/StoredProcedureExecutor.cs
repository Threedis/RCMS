using System.Data;
using Dapper;
using Inventory.Common.Exceptions;
using Inventory.DAL.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace Inventory.DAL.StoredProcedures;

/// <summary>
/// Default <see cref="IStoredProcedureExecutor"/> implementation.
/// <para>
/// It borrows the <see cref="ApplicationDbContext"/>'s connection so that both
/// stored-procedure work and EF Core work participate in the same transaction,
/// and it honours the context's configured command timeout.
/// </para>
/// </summary>
public sealed class StoredProcedureExecutor : IStoredProcedureExecutor
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<StoredProcedureExecutor> _logger;

    public StoredProcedureExecutor(ApplicationDbContext context, ILogger<StoredProcedureExecutor> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<T>> QueryAsync<T>(
        string procedureName,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteGuardedAsync(procedureName, async connection =>
        {
            var command = BuildCommand(procedureName, parameters, cancellationToken);
            var rows = await connection.QueryAsync<T>(command).ConfigureAwait(false);
            return (IReadOnlyList<T>)rows.AsList();
        }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<T?> QuerySingleOrDefaultAsync<T>(
        string procedureName,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteGuardedAsync(procedureName, async connection =>
        {
            var command = BuildCommand(procedureName, parameters, cancellationToken);
            return await connection.QueryFirstOrDefaultAsync<T>(command).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<T?> ExecuteScalarAsync<T>(
        string procedureName,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteGuardedAsync(procedureName, async connection =>
        {
            var command = BuildCommand(procedureName, parameters, cancellationToken);
            return await connection.ExecuteScalarAsync<T>(command).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<T> Items, int TotalCount)> QueryPagedAsync<T>(
        string procedureName,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var dynamicParameters = ToDynamicParameters(parameters);
        dynamicParameters.Add(SpParameters.TotalCount, dbType: DbType.Int32, direction: ParameterDirection.Output);

        return await ExecuteGuardedAsync(procedureName, async connection =>
        {
            var command = BuildCommand(procedureName, dynamicParameters, cancellationToken);
            var rows = await connection.QueryAsync<T>(command).ConfigureAwait(false);
            var total = dynamicParameters.Get<int?>(SpParameters.TotalCount) ?? 0;
            return ((IReadOnlyList<T>)rows.AsList(), total);
        }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<SpResult> ExecuteAsync(
        string procedureName,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var dynamicParameters = ToDynamicParameters(parameters);

        // The three-parameter contract every write procedure implements.
        AddOutputIfMissing(dynamicParameters, SpParameters.ReturnCode, DbType.Int32);
        AddOutputIfMissing(dynamicParameters, SpParameters.Message, DbType.String, 500);
        AddOutputIfMissing(dynamicParameters, SpParameters.NewId, DbType.Int32);
        AddOutputIfMissing(dynamicParameters, SpParameters.GeneratedNumber, DbType.String, 30);

        return await ExecuteGuardedAsync(procedureName, async connection =>
        {
            var command = BuildCommand(procedureName, dynamicParameters, cancellationToken);
            await connection.ExecuteAsync(command).ConfigureAwait(false);

            var result = new SpResult
            {
                ReturnCode = dynamicParameters.Get<int?>(SpParameters.ReturnCode) ?? 0,
                Message = dynamicParameters.Get<string?>(SpParameters.Message) ?? string.Empty,
                NewId = dynamicParameters.Get<int?>(SpParameters.NewId),
                GeneratedNumber = dynamicParameters.Get<string?>(SpParameters.GeneratedNumber)
            };

            if (!result.IsSuccess)
            {
                _logger.LogWarning(
                    "Stored procedure {Procedure} returned {ReturnCode}: {Message}",
                    procedureName, result.ReturnCode, result.Message);
            }

            return result;
        }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<TResult> QueryMultipleAsync<TResult>(
        string procedureName,
        Func<IMultipleResultReader, Task<TResult>> reader,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reader);

        return await ExecuteGuardedAsync(procedureName, async connection =>
        {
            var command = BuildCommand(procedureName, parameters, cancellationToken);
            using var grid = await connection.QueryMultipleAsync(command).ConfigureAwait(false);
            return await reader(new DapperMultipleResultReader(grid)).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    // -----------------------------------------------------------------------
    // Infrastructure
    // -----------------------------------------------------------------------

    /// <summary>
    /// Opens the shared connection if required, runs <paramref name="action"/>
    /// and translates provider exceptions into <see cref="DataAccessException"/>.
    /// </summary>
    private async Task<TResult> ExecuteGuardedAsync<TResult>(
        string procedureName,
        Func<System.Data.Common.DbConnection, Task<TResult>> action)
    {
        var connection = _context.Database.GetDbConnection();
        var openedHere = false;

        try
        {
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync().ConfigureAwait(false);
                openedHere = true;
            }

            return await action(connection).ConfigureAwait(false);
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, "SQL error {Number} executing {Procedure}", ex.Number, procedureName);
            throw new DataAccessException(procedureName, ex.Message, ex);
        }
        finally
        {
            // Leave the connection open when EF or an ambient transaction owns it.
            if (openedHere && _context.Database.CurrentTransaction is null)
            {
                await connection.CloseAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Builds the Dapper command, attaching the ambient transaction (if any) so
    /// the procedure participates in the current unit of work.
    /// </summary>
    private CommandDefinition BuildCommand(string procedureName, object? parameters, CancellationToken cancellationToken)
    {
        IDbContextTransaction? current = _context.Database.CurrentTransaction;
        var transaction = current?.GetDbTransaction();
        var timeout = _context.Database.GetCommandTimeout();

        return new CommandDefinition(
            commandText: procedureName,
            parameters: parameters,
            transaction: transaction,
            commandTimeout: timeout,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken);
    }

    private static DynamicParameters ToDynamicParameters(object? parameters)
        => parameters switch
        {
            null => new DynamicParameters(),
            DynamicParameters existing => existing,
            _ => new DynamicParameters(parameters)
        };

    private static void AddOutputIfMissing(DynamicParameters parameters, string name, DbType dbType, int? size = null)
    {
        if (parameters.ParameterNames.Contains(name.TrimStart('@'), StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        parameters.Add(name, dbType: dbType, direction: ParameterDirection.Output, size: size);
    }

    /// <summary>Adapts Dapper's <see cref="SqlMapper.GridReader"/> to the DAL-owned interface.</summary>
    private sealed class DapperMultipleResultReader : IMultipleResultReader
    {
        private readonly SqlMapper.GridReader _grid;

        public DapperMultipleResultReader(SqlMapper.GridReader grid) => _grid = grid;

        public async Task<IReadOnlyList<T>> ReadAsync<T>()
        {
            var rows = await _grid.ReadAsync<T>().ConfigureAwait(false);
            return rows.AsList();
        }

        public async Task<T?> ReadSingleOrDefaultAsync<T>()
            => await _grid.ReadFirstOrDefaultAsync<T>().ConfigureAwait(false);
    }
}
