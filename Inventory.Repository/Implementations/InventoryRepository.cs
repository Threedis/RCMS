using System.Data;
using Dapper;
using Inventory.Common.Constants;
using Inventory.Common.Enums;
using Inventory.Common.Models;
using Inventory.DAL.StoredProcedures;
using Inventory.Entities.Dtos;
using Inventory.Repository.Interfaces;

namespace Inventory.Repository.Implementations;

/// <summary>
/// Inventory document repository.
/// <para>
/// A goods receipt or a material issue touches the header, its lines, the stock
/// ledger, the item's average cost, the approval trail and the audit log. All of
/// that must succeed or fail as one unit, so each operation is a single stored
/// procedure call: the header scalars go in as parameters and the lines go in as
/// a table-valued parameter, giving one round trip and one SQL transaction.
/// </para>
/// </summary>
public sealed class InventoryRepository : IInventoryRepository
{
    private readonly IStoredProcedureExecutor _executor;

    public InventoryRepository(IStoredProcedureExecutor executor)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }

    // -----------------------------------------------------------------------
    // Goods Receipt Note
    // -----------------------------------------------------------------------

    /// <inheritdoc />
    public async Task<PagedResult<GrnListDto>> GetPagedGrnsAsync(
        PagedRequest request,
        ReportFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(filter);

        var parameters = BuildPagingParameters(request);
        parameters.Add("@FromDate", filter.FromDate, DbType.Date);
        parameters.Add("@ToDate", filter.ToDate, DbType.Date);
        parameters.Add("@VendorId", filter.VendorId);
        parameters.Add("@WarehouseId", filter.WarehouseId);
        parameters.Add("@Status", filter.Status);

        var (items, total) = await _executor
            .QueryPagedAsync<GrnListDto>(StoredProcedures.GetGRNs, parameters, cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<GrnListDto>(items, total, request.PageNumber, request.PageSize);
    }

    /// <inheritdoc />
    public async Task<GrnDto?> GetGrnAsync(int grnId, CancellationToken cancellationToken = default)
        => await _executor.QueryMultipleAsync<GrnDto?>(
            StoredProcedures.GetGRNById,
            async reader =>
            {
                var header = await reader.ReadSingleOrDefaultAsync<GrnDto>().ConfigureAwait(false);

                if (header is null)
                {
                    return null;
                }

                header.Details = (await reader.ReadAsync<GrnDetailDto>().ConfigureAwait(false)).ToList();
                header.Attachments = (await reader.ReadAsync<AttachmentDto>().ConfigureAwait(false)).ToList();
                header.ApprovalTrail = (await reader.ReadAsync<ApprovalHistoryDto>().ConfigureAwait(false)).ToList();

                return header;
            },
            new { GrnId = grnId },
            cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<SpResult> SaveGrnAsync(
        GrnDto grn,
        int userId,
        bool isUpdate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(grn);

        var parameters = new DynamicParameters();
        parameters.Add("@GrnId", grn.Id);
        parameters.Add("@GrnDate", grn.GrnDate, DbType.Date);
        parameters.Add("@PurchaseOrderNumber", grn.PurchaseOrderNumber);
        parameters.Add("@PurchaseOrderDate", grn.PurchaseOrderDate, DbType.Date);
        parameters.Add("@DeliveryChallanNumber", grn.DeliveryChallanNumber);
        parameters.Add("@DeliveryChallanDate", grn.DeliveryChallanDate, DbType.Date);
        parameters.Add("@InvoiceNumber", grn.InvoiceNumber);
        parameters.Add("@InvoiceDate", grn.InvoiceDate, DbType.Date);
        parameters.Add("@VendorId", grn.VendorId);
        parameters.Add("@CourierId", grn.CourierId);
        parameters.Add("@ConsignmentNumber", grn.ConsignmentNumber);
        parameters.Add("@WarehouseId", grn.WarehouseId);
        parameters.Add("@Remarks", grn.Remarks);
        parameters.Add("@CurrentUserId", userId);
        parameters.Add("@Details", BuildGrnDetailTable(grn.Details).AsTableValuedParameter("dbo.GrnDetailType"));

        var procedure = isUpdate ? StoredProcedures.UpdateGRN : StoredProcedures.InsertGRN;

        return await _executor.ExecuteAsync(procedure, parameters, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<SpResult> DeleteGrnAsync(int grnId, int userId, CancellationToken cancellationToken = default)
        => await _executor.ExecuteAsync(
            StoredProcedures.DeleteGRN,
            new { GrnId = grnId, CurrentUserId = userId },
            cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<SpResult> SubmitGrnAsync(
        int grnId,
        int userId,
        string? remarks,
        CancellationToken cancellationToken = default)
        => await _executor.ExecuteAsync(
            StoredProcedures.SubmitGRN,
            new { GrnId = grnId, CurrentUserId = userId, Remarks = remarks },
            cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<SpResult> ApproveGrnAsync(
        int grnId,
        int userId,
        bool approve,
        string? remarks,
        CancellationToken cancellationToken = default)
        => await _executor.ExecuteAsync(
            StoredProcedures.ApproveGRN,
            new { GrnId = grnId, CurrentUserId = userId, Approve = approve, Remarks = remarks },
            cancellationToken).ConfigureAwait(false);

    // -----------------------------------------------------------------------
    // Material Issue
    // -----------------------------------------------------------------------

    /// <inheritdoc />
    public async Task<PagedResult<IssueListDto>> GetPagedIssuesAsync(
        PagedRequest request,
        ReportFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(filter);

        var parameters = BuildPagingParameters(request);
        parameters.Add("@FromDate", filter.FromDate, DbType.Date);
        parameters.Add("@ToDate", filter.ToDate, DbType.Date);
        parameters.Add("@DepartmentId", filter.DepartmentId);
        parameters.Add("@SiteId", filter.SiteId);
        parameters.Add("@EngineerId", filter.EngineerId);
        parameters.Add("@WarehouseId", filter.WarehouseId);
        parameters.Add("@Status", filter.Status);

        var (items, total) = await _executor
            .QueryPagedAsync<IssueListDto>(StoredProcedures.GetInventoryIssues, parameters, cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<IssueListDto>(items, total, request.PageNumber, request.PageSize);
    }

    /// <inheritdoc />
    public async Task<IssueDto?> GetIssueAsync(int issueId, CancellationToken cancellationToken = default)
        => await _executor.QueryMultipleAsync<IssueDto?>(
            StoredProcedures.GetInventoryIssueById,
            async reader =>
            {
                var header = await reader.ReadSingleOrDefaultAsync<IssueDto>().ConfigureAwait(false);

                if (header is null)
                {
                    return null;
                }

                header.Details = (await reader.ReadAsync<IssueDetailDto>().ConfigureAwait(false)).ToList();
                header.Attachments = (await reader.ReadAsync<AttachmentDto>().ConfigureAwait(false)).ToList();
                header.ApprovalTrail = (await reader.ReadAsync<ApprovalHistoryDto>().ConfigureAwait(false)).ToList();

                return header;
            },
            new { IssueId = issueId },
            cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<SpResult> SaveIssueAsync(
        IssueDto issue,
        int userId,
        bool isUpdate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(issue);

        var parameters = new DynamicParameters();
        parameters.Add("@IssueId", issue.Id);
        parameters.Add("@IssueDate", issue.IssueDate, DbType.Date);
        parameters.Add("@DepartmentId", issue.DepartmentId);
        parameters.Add("@SiteId", issue.SiteId);
        parameters.Add("@EngineerId", issue.EngineerId);
        parameters.Add("@WarehouseId", issue.WarehouseId);
        parameters.Add("@ProjectName", issue.ProjectName);
        parameters.Add("@WorkOrderNumber", issue.WorkOrderNumber);
        parameters.Add("@Purpose", issue.Purpose);
        parameters.Add("@Remarks", issue.Remarks);
        parameters.Add("@CurrentUserId", userId);
        parameters.Add("@Details", BuildIssueDetailTable(issue.Details).AsTableValuedParameter("dbo.IssueDetailType"));

        var procedure = isUpdate ? StoredProcedures.UpdateInventoryIssue : StoredProcedures.InsertInventoryIssue;

        return await _executor.ExecuteAsync(procedure, parameters, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<SpResult> DeleteIssueAsync(int issueId, int userId, CancellationToken cancellationToken = default)
        => await _executor.ExecuteAsync(
            StoredProcedures.DeleteInventoryIssue,
            new { IssueId = issueId, CurrentUserId = userId },
            cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<SpResult> SubmitIssueAsync(
        int issueId,
        int userId,
        string? remarks,
        CancellationToken cancellationToken = default)
        => await _executor.ExecuteAsync(
            StoredProcedures.SubmitInventoryIssue,
            new { IssueId = issueId, CurrentUserId = userId, Remarks = remarks },
            cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<SpResult> ApproveIssueAsync(
        ApprovalRequestDto request,
        int userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var parameters = new DynamicParameters();
        parameters.Add("@IssueId", request.DocumentId);
        parameters.Add("@CurrentUserId", userId);
        parameters.Add("@Approve", request.Approve);
        parameters.Add("@Remarks", request.Remarks);
        parameters.Add("@Lines", BuildLineQuantityTable(
                request.Lines.Select(l => (l.DetailId, l.QuantityApproved)))
            .AsTableValuedParameter("dbo.LineQuantityType"));

        return await _executor
            .ExecuteAsync(StoredProcedures.ApproveInventoryIssue, parameters, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<SpResult> DispatchIssueAsync(
        DispatchRequestDto request,
        int userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var parameters = new DynamicParameters();
        parameters.Add("@IssueId", request.IssueId);
        parameters.Add("@CurrentUserId", userId);
        parameters.Add("@CourierId", request.CourierId);
        parameters.Add("@TrackingNumber", request.TrackingNumber);
        parameters.Add("@DispatchDate", request.DispatchDate, DbType.Date);
        parameters.Add("@ReceiverName", request.ReceiverName);
        parameters.Add("@Remarks", request.Remarks);
        parameters.Add("@Lines", BuildLineQuantityTable(
                request.Lines.Select(l => (l.DetailId, l.QuantityIssued)))
            .AsTableValuedParameter("dbo.LineQuantityType"));

        return await _executor
            .ExecuteAsync(StoredProcedures.DispatchInventoryIssue, parameters, cancellationToken)
            .ConfigureAwait(false);
    }

    // -----------------------------------------------------------------------
    // Stock
    // -----------------------------------------------------------------------

    /// <inheritdoc />
    public async Task<PagedResult<CurrentStockDto>> GetCurrentStockAsync(
        PagedRequest request,
        ReportFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(filter);

        var parameters = BuildPagingParameters(request);
        parameters.Add("@ItemId", filter.ItemId);
        parameters.Add("@CategoryId", filter.CategoryId);
        parameters.Add("@WarehouseId", filter.WarehouseId);
        parameters.Add("@SiteId", filter.SiteId);
        parameters.Add("@ItemType", filter.ItemType);

        var (items, total) = await _executor
            .QueryPagedAsync<CurrentStockDto>(StoredProcedures.GetCurrentStock, parameters, cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<CurrentStockDto>(items, total, request.PageNumber, request.PageSize);
    }

    /// <inheritdoc />
    public async Task<PagedResult<StockLedgerDto>> GetStockLedgerAsync(
        PagedRequest request,
        ReportFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(filter);

        var parameters = BuildPagingParameters(request);
        parameters.Add("@ItemId", filter.ItemId);
        parameters.Add("@WarehouseId", filter.WarehouseId);
        parameters.Add("@FromDate", filter.EffectiveFrom, DbType.Date);
        parameters.Add("@ToDate", filter.EffectiveTo, DbType.Date);

        var (items, total) = await _executor
            .QueryPagedAsync<StockLedgerDto>(StoredProcedures.GetStockLedger, parameters, cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<StockLedgerDto>(items, total, request.PageNumber, request.PageSize);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LowStockDto>> GetLowStockItemsAsync(
        int? warehouseId = null,
        int maxRows = 100,
        CancellationToken cancellationToken = default)
        => await _executor.QueryAsync<LowStockDto>(
            StoredProcedures.GetLowStockItems,
            new { WarehouseId = warehouseId, MaxRows = maxRows },
            cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<ItemStockSnapshotDto?> GetItemBalanceAsync(
        int itemId,
        int warehouseId,
        CancellationToken cancellationToken = default)
        => await _executor.QuerySingleOrDefaultAsync<ItemStockSnapshotDto>(
            StoredProcedures.GetItemStockBalance,
            new { ItemId = itemId, WarehouseId = warehouseId },
            cancellationToken).ConfigureAwait(false);

    // -----------------------------------------------------------------------
    // Approvals
    // -----------------------------------------------------------------------

    /// <inheritdoc />
    public async Task<IReadOnlyList<PendingApprovalDto>> GetPendingApprovalsAsync(
        int userId,
        DocumentType? documentType = null,
        CancellationToken cancellationToken = default)
        => await _executor.QueryAsync<PendingApprovalDto>(
            StoredProcedures.GetPendingApprovals,
            new { CurrentUserId = userId, DocumentType = (int?)documentType },
            cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ApprovalHistoryDto>> GetApprovalTrailAsync(
        DocumentType documentType,
        int documentId,
        CancellationToken cancellationToken = default)
        => await _executor.QueryAsync<ApprovalHistoryDto>(
            "dbo.sp_GetApprovalTrail",
            new { DocumentType = (int)documentType, DocumentId = documentId },
            cancellationToken).ConfigureAwait(false);

    // -----------------------------------------------------------------------
    // Table-valued parameter builders
    // -----------------------------------------------------------------------

    private static DynamicParameters BuildPagingParameters(PagedRequest request)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@PageNumber", request.PageNumber);
        parameters.Add("@PageSize", request.PageSize);
        parameters.Add("@SearchTerm", request.SearchTerm);
        parameters.Add("@SortColumn", request.SortColumn);
        parameters.Add("@SortDirection", request.SafeSortDirection);
        return parameters;
    }

    /// <summary>
    /// Shapes the GRN lines into the <c>dbo.GrnDetailType</c> table type.
    /// Column order must match the type definition in <c>02_Types.sql</c>.
    /// </summary>
    private static DataTable BuildGrnDetailTable(IEnumerable<GrnDetailDto> details)
    {
        var table = new DataTable();
        table.Columns.Add("LineNumber", typeof(int));
        table.Columns.Add("ItemId", typeof(int));
        table.Columns.Add("PartNumber", typeof(string));
        table.Columns.Add("QuantityReceived", typeof(decimal));
        table.Columns.Add("QuantityAccepted", typeof(decimal));
        table.Columns.Add("QuantityRejected", typeof(decimal));
        table.Columns.Add("RejectionReason", typeof(string));
        table.Columns.Add("BatchNumber", typeof(string));
        table.Columns.Add("SerialNumber", typeof(string));
        table.Columns.Add("ManufacturingDate", typeof(DateTime));
        table.Columns.Add("ExpiryDate", typeof(DateTime));
        table.Columns.Add("UnitCost", typeof(decimal));
        table.Columns.Add("DiscountPercent", typeof(decimal));
        table.Columns.Add("TaxRate", typeof(decimal));
        table.Columns.Add("ShelfLocation", typeof(string));
        table.Columns.Add("Remarks", typeof(string));

        var line = 1;

        foreach (var detail in details)
        {
            table.Rows.Add(
                detail.LineNumber > 0 ? detail.LineNumber : line,
                detail.ItemId,
                (object?)detail.PartNumber ?? DBNull.Value,
                detail.QuantityReceived,
                detail.QuantityAccepted,
                detail.QuantityRejected,
                (object?)detail.RejectionReason ?? DBNull.Value,
                (object?)detail.BatchNumber ?? DBNull.Value,
                (object?)detail.SerialNumber ?? DBNull.Value,
                (object?)detail.ManufacturingDate ?? DBNull.Value,
                (object?)detail.ExpiryDate ?? DBNull.Value,
                detail.UnitCost,
                detail.DiscountPercent,
                detail.TaxRate,
                (object?)detail.ShelfLocation ?? DBNull.Value,
                (object?)detail.Remarks ?? DBNull.Value);

            line++;
        }

        return table;
    }

    /// <summary>Shapes the issue lines into the <c>dbo.IssueDetailType</c> table type.</summary>
    private static DataTable BuildIssueDetailTable(IEnumerable<IssueDetailDto> details)
    {
        var table = new DataTable();
        table.Columns.Add("LineNumber", typeof(int));
        table.Columns.Add("ItemId", typeof(int));
        table.Columns.Add("PartNumber", typeof(string));
        table.Columns.Add("QuantityRequested", typeof(decimal));
        table.Columns.Add("BatchNumber", typeof(string));
        table.Columns.Add("SerialNumber", typeof(string));
        table.Columns.Add("Remarks", typeof(string));

        var line = 1;

        foreach (var detail in details)
        {
            table.Rows.Add(
                detail.LineNumber > 0 ? detail.LineNumber : line,
                detail.ItemId,
                (object?)detail.PartNumber ?? DBNull.Value,
                detail.QuantityRequested,
                (object?)detail.BatchNumber ?? DBNull.Value,
                (object?)detail.SerialNumber ?? DBNull.Value,
                (object?)detail.Remarks ?? DBNull.Value);

            line++;
        }

        return table;
    }

    /// <summary>Shapes per-line quantities into the <c>dbo.LineQuantityType</c> table type.</summary>
    private static DataTable BuildLineQuantityTable(IEnumerable<(int DetailId, decimal Quantity)> lines)
    {
        var table = new DataTable();
        table.Columns.Add("DetailId", typeof(int));
        table.Columns.Add("Quantity", typeof(decimal));

        foreach (var (detailId, quantity) in lines)
        {
            table.Rows.Add(detailId, quantity);
        }

        return table;
    }
}
