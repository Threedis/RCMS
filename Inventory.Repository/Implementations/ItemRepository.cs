using Dapper;
using Inventory.Common.Constants;
using Inventory.Common.Models;
using Inventory.DAL.Context;
using Inventory.DAL.StoredProcedures;
using Inventory.Entities.Dtos;
using Inventory.Entities.Models;
using Inventory.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Repository.Implementations;

/// <summary>
/// Item master repository. Reads that feed grids and pickers are served by
/// stored procedures (they join the ledger for balances); simple existence
/// checks stay in LINQ because they need no joins.
/// </summary>
public sealed class ItemRepository : GenericRepository<Item>, IItemRepository
{
    private readonly IStoredProcedureExecutor _executor;

    public ItemRepository(ApplicationDbContext context, IStoredProcedureExecutor executor)
        : base(context)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }

    /// <inheritdoc />
    public async Task<PagedResult<ItemListDto>> GetPagedItemsAsync(
        PagedRequest request,
        int? categoryId = null,
        int? itemType = null,
        bool? lowStockOnly = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var parameters = new DynamicParameters();
        parameters.Add("@PageNumber", request.PageNumber);
        parameters.Add("@PageSize", request.PageSize);
        parameters.Add("@SearchTerm", request.SearchTerm);
        parameters.Add("@SortColumn", request.SortColumn);
        parameters.Add("@SortDirection", request.SafeSortDirection);
        parameters.Add("@CategoryId", categoryId);
        parameters.Add("@ItemType", itemType);
        parameters.Add("@LowStockOnly", lowStockOnly);

        var (items, total) = await _executor
            .QueryPagedAsync<ItemListDto>(StoredProcedures.GetItems, parameters, cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<ItemListDto>(items, total, request.PageNumber, request.PageSize);
    }

    /// <inheritdoc />
    public async Task<ItemDto?> GetItemAsync(int itemId, CancellationToken cancellationToken = default)
        => await _executor
            .QuerySingleOrDefaultAsync<ItemDto>(
                StoredProcedures.GetItemById,
                new { ItemId = itemId },
                cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<SpResult> InsertItemAsync(ItemDto item, int userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        return await _executor
            .ExecuteAsync(StoredProcedures.InsertItem, BuildItemParameters(item, userId), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<SpResult> UpdateItemAsync(ItemDto item, int userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        return await _executor
            .ExecuteAsync(StoredProcedures.UpdateItem, BuildItemParameters(item, userId), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<SpResult> DeleteItemAsync(int itemId, int userId, CancellationToken cancellationToken = default)
        => await _executor
            .ExecuteAsync(
                StoredProcedures.DeleteItem,
                new { ItemId = itemId, CurrentUserId = userId },
                cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ItemStockSnapshotDto>> SearchItemsForDocumentAsync(
        string? term,
        int warehouseId,
        int maxResults = 20,
        CancellationToken cancellationToken = default)
        => await _executor
            .QueryAsync<ItemStockSnapshotDto>(
                StoredProcedures.SearchInventory,
                new { SearchTerm = term, WarehouseId = warehouseId, MaxResults = maxResults },
                cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<ItemStockSnapshotDto?> GetByBarcodeAsync(
        string barcode,
        int warehouseId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(barcode))
        {
            return null;
        }

        var matches = await _executor
            .QueryAsync<ItemStockSnapshotDto>(
                StoredProcedures.SearchInventory,
                new { SearchTerm = barcode.Trim(), WarehouseId = warehouseId, MaxResults = 1, ExactMatch = true },
                cancellationToken)
            .ConfigureAwait(false);

        return matches.FirstOrDefault();
    }

    /// <inheritdoc />
    public async Task<bool> IsItemCodeTakenAsync(
        string itemCode,
        int excludeId = 0,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(itemCode))
        {
            return false;
        }

        var normalized = itemCode.Trim();

        return await DbSet.AsNoTracking()
            .AnyAsync(i => !i.IsDeleted && i.ItemCode == normalized && i.Id != excludeId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Maps the DTO onto the insert/update procedure's parameter list.</summary>
    private static DynamicParameters BuildItemParameters(ItemDto item, int userId)
    {
        var parameters = new DynamicParameters();

        parameters.Add("@ItemId", item.Id);
        parameters.Add("@ItemCode", item.ItemCode);
        parameters.Add("@ItemName", item.ItemName);
        parameters.Add("@PartNumber", item.PartNumber);
        parameters.Add("@AlternatePartNumber", item.AlternatePartNumber);
        parameters.Add("@HsnCode", item.HsnCode);
        parameters.Add("@Description", item.Description);
        parameters.Add("@Specification", item.Specification);
        parameters.Add("@CategoryId", item.CategoryId);
        parameters.Add("@UnitId", item.UnitId);
        parameters.Add("@ManufacturerId", item.ManufacturerId);
        parameters.Add("@ItemType", (int)item.ItemType);
        parameters.Add("@Barcode", item.Barcode);
        parameters.Add("@ReorderLevel", item.ReorderLevel);
        parameters.Add("@ReorderQuantity", item.ReorderQuantity);
        parameters.Add("@MinimumStock", item.MinimumStock);
        parameters.Add("@MaximumStock", item.MaximumStock);
        parameters.Add("@StandardCost", item.StandardCost);
        parameters.Add("@ShelfLocation", item.ShelfLocation);
        parameters.Add("@IsBatchTracked", item.IsBatchTracked);
        parameters.Add("@IsSerialTracked", item.IsSerialTracked);
        parameters.Add("@ShelfLifeDays", item.ShelfLifeDays);
        parameters.Add("@TaxRate", item.TaxRate);
        parameters.Add("@IsActive", item.IsActive);
        parameters.Add("@CurrentUserId", userId);

        return parameters;
    }
}

/// <summary>Vendor master repository.</summary>
public sealed class VendorRepository : GenericRepository<Vendor>, IVendorRepository
{
    private readonly IStoredProcedureExecutor _executor;

    public VendorRepository(ApplicationDbContext context, IStoredProcedureExecutor executor)
        : base(context)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }

    /// <inheritdoc />
    public async Task<PagedResult<VendorDto>> GetPagedVendorsAsync(
        PagedRequest request,
        bool? blacklistedOnly = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var parameters = new DynamicParameters();
        parameters.Add("@PageNumber", request.PageNumber);
        parameters.Add("@PageSize", request.PageSize);
        parameters.Add("@SearchTerm", request.SearchTerm);
        parameters.Add("@SortColumn", request.SortColumn);
        parameters.Add("@SortDirection", request.SafeSortDirection);
        parameters.Add("@BlacklistedOnly", blacklistedOnly);

        var (items, total) = await _executor
            .QueryPagedAsync<VendorDto>(StoredProcedures.GetVendors, parameters, cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<VendorDto>(items, total, request.PageNumber, request.PageSize);
    }

    /// <inheritdoc />
    public async Task<VendorDto?> GetVendorAsync(int vendorId, CancellationToken cancellationToken = default)
        => await _executor
            .QuerySingleOrDefaultAsync<VendorDto>(
                StoredProcedures.GetVendorById,
                new { VendorId = vendorId },
                cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<SpResult> InsertVendorAsync(VendorDto vendor, int userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(vendor);
        return await _executor
            .ExecuteAsync(StoredProcedures.InsertVendor, BuildVendorParameters(vendor, userId), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<SpResult> UpdateVendorAsync(VendorDto vendor, int userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(vendor);
        return await _executor
            .ExecuteAsync(StoredProcedures.UpdateVendor, BuildVendorParameters(vendor, userId), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<SpResult> DeleteVendorAsync(int vendorId, int userId, CancellationToken cancellationToken = default)
        => await _executor
            .ExecuteAsync(
                StoredProcedures.DeleteVendor,
                new { VendorId = vendorId, CurrentUserId = userId },
                cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<bool> IsGstNumberTakenAsync(
        string gstNumber,
        int excludeId = 0,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(gstNumber))
        {
            return false;
        }

        var normalized = gstNumber.Trim();

        return await DbSet.AsNoTracking()
            .AnyAsync(v => !v.IsDeleted && v.GstNumber == normalized && v.Id != excludeId, cancellationToken)
            .ConfigureAwait(false);
    }

    private static DynamicParameters BuildVendorParameters(VendorDto vendor, int userId)
    {
        var parameters = new DynamicParameters();

        parameters.Add("@VendorId", vendor.Id);
        parameters.Add("@Code", vendor.Code);
        parameters.Add("@Name", vendor.Name);
        parameters.Add("@Description", vendor.Description);
        parameters.Add("@Address", vendor.Address);
        parameters.Add("@City", vendor.City);
        parameters.Add("@State", vendor.State);
        parameters.Add("@PinCode", vendor.PinCode);
        parameters.Add("@ContactPerson", vendor.ContactPerson);
        parameters.Add("@ContactNumber", vendor.ContactNumber);
        parameters.Add("@Email", vendor.Email);
        parameters.Add("@GstNumber", vendor.GstNumber);
        parameters.Add("@PanNumber", vendor.PanNumber);
        parameters.Add("@BankName", vendor.BankName);
        parameters.Add("@BankAccountNumber", vendor.BankAccountNumber);
        parameters.Add("@IfscCode", vendor.IfscCode);
        parameters.Add("@CreditDays", vendor.CreditDays);
        parameters.Add("@Rating", vendor.Rating);
        parameters.Add("@IsBlacklisted", vendor.IsBlacklisted);
        parameters.Add("@DisplayOrder", vendor.DisplayOrder);
        parameters.Add("@IsActive", vendor.IsActive);
        parameters.Add("@CurrentUserId", userId);

        return parameters;
    }
}
