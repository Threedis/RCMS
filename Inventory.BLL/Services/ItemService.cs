using Inventory.BLL.Interfaces;
using Inventory.Common.Constants;
using Inventory.Common.Enums;
using Inventory.Common.Extensions;
using Inventory.Common.Models;
using Inventory.Common.Security;
using Inventory.Entities.Dtos;
using Inventory.Repository.UnitOfWork;
using Inventory.Services.Caching;
using Inventory.Services.Export;
using Inventory.Services.Import;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Inventory.BLL.Services;

/// <summary>
/// Item master business service: validation, code generation, export and the
/// Excel import wizard. Persistence is delegated to the item repository, which
/// calls the item stored procedures.
/// </summary>
public sealed class ItemService : IItemService
{
    // Column headings the import wizard expects. They double as the template
    // headings, so the template and the parser can never drift apart.
    private const string ColItemName = "Item Name";
    private const string ColPartNumber = "Part Number";
    private const string ColCategory = "Category Code";
    private const string ColUnit = "Unit Code";
    private const string ColManufacturer = "Manufacturer Code";
    private const string ColItemType = "Item Type";
    private const string ColBarcode = "Barcode";
    private const string ColHsn = "HSN Code";
    private const string ColReorderLevel = "Reorder Level";
    private const string ColReorderQty = "Reorder Quantity";
    private const string ColMinStock = "Minimum Stock";
    private const string ColMaxStock = "Maximum Stock";
    private const string ColStandardCost = "Standard Cost";
    private const string ColTaxRate = "Tax Rate %";
    private const string ColShelf = "Shelf Location";
    private const string ColDescription = "Description";

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _audit;
    private readonly IExportService _export;
    private readonly IExcelImportReader _importReader;
    private readonly ICacheService _cache;
    private readonly ILogger<ItemService> _logger;

    public ItemService(
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditService audit,
        IExportService export,
        IExcelImportReader importReader,
        ICacheService cache,
        ILogger<ItemService> logger)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
        _export = export ?? throw new ArgumentNullException(nameof(export));
        _importReader = importReader ?? throw new ArgumentNullException(nameof(importReader));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Task<PagedResult<ItemListDto>> GetPagedAsync(
        PagedRequest request,
        int? categoryId = null,
        int? itemType = null,
        bool? lowStockOnly = null,
        CancellationToken cancellationToken = default)
        => _unitOfWork.Items.GetPagedItemsAsync(request, categoryId, itemType, lowStockOnly, cancellationToken);

    /// <inheritdoc />
    public Task<ItemDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => _unitOfWork.Items.GetItemAsync(id, cancellationToken);

    /// <inheritdoc />
    public async Task<ServiceResult<int>> CreateAsync(ItemDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var validation = await ValidateAsync(dto, isUpdate: false, cancellationToken).ConfigureAwait(false);

        if (!validation.Succeeded)
        {
            return ServiceResult<int>.From(validation);
        }

        var result = await _unitOfWork.Items
            .InsertItemAsync(dto, _currentUser.UserId, cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return ServiceResult<int>.Failure(result.Message, result.ReturnCode);
        }

        _cache.Remove(CacheKeys.Items);

        await _audit.LogAsync(
            AuditAction.Create,
            "Item",
            result.NewId?.ToString(),
            $"Created item '{dto.ItemName}' ({result.GeneratedNumber ?? dto.ItemCode}).",
            newValues: Serialize(dto),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return ServiceResult<int>.Success(
            result.NewId ?? 0,
            $"Item {result.GeneratedNumber ?? dto.ItemCode} created successfully.");
    }

    /// <inheritdoc />
    public async Task<ServiceResult> UpdateAsync(ItemDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var existing = await _unitOfWork.Items.GetItemAsync(dto.Id, cancellationToken).ConfigureAwait(false);

        if (existing is null)
        {
            return ServiceResult.Failure("The item was not found.", -404);
        }

        var validation = await ValidateAsync(dto, isUpdate: true, cancellationToken).ConfigureAwait(false);

        if (!validation.Succeeded)
        {
            return validation;
        }

        var result = await _unitOfWork.Items
            .UpdateItemAsync(dto, _currentUser.UserId, cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return ServiceResult.Failure(result.Message, result.ReturnCode);
        }

        _cache.Remove(CacheKeys.Items);

        await _audit.LogAsync(
            AuditAction.Update,
            "Item",
            dto.Id.ToString(),
            $"Updated item '{dto.ItemName}'.",
            oldValues: Serialize(existing),
            newValues: Serialize(dto),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return ServiceResult.Success("Item updated successfully.");
    }

    /// <inheritdoc />
    public async Task<ServiceResult> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var existing = await _unitOfWork.Items.GetItemAsync(id, cancellationToken).ConfigureAwait(false);

        if (existing is null)
        {
            return ServiceResult.Failure("The item was not found.", -404);
        }

        // The procedure blocks the delete when stock or transaction history
        // exists; that rule belongs with the data, not here.
        var result = await _unitOfWork.Items
            .DeleteItemAsync(id, _currentUser.UserId, cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return ServiceResult.Failure(result.Message, result.ReturnCode);
        }

        _cache.Remove(CacheKeys.Items);

        await _audit.LogAsync(
            AuditAction.Delete,
            "Item",
            id.ToString(),
            $"Deleted item '{existing.ItemName}'.",
            oldValues: Serialize(existing),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return ServiceResult.Success("Item deleted successfully.");
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LookupDto>> GetLookupAsync(CancellationToken cancellationToken = default)
        => await _cache.GetOrCreateAsync(
            CacheKeys.Items,
            async () => (IReadOnlyList<LookupDto>)await _unitOfWork.Items.Query()
                .Where(i => i.IsActive && !i.IsDeleted)
                .OrderBy(i => i.ItemName)
                .Select(i => new LookupDto
                {
                    Id = i.Id,
                    Text = i.ItemName,
                    Code = i.ItemCode,
                    ParentId = i.CategoryId,
                    Extra = i.PartNumber
                })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false),
            CacheKeys.LookupDuration,
            cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public Task<IReadOnlyList<ItemStockSnapshotDto>> SearchForDocumentAsync(
        string? term,
        int warehouseId,
        CancellationToken cancellationToken = default)
        => _unitOfWork.Items.SearchItemsForDocumentAsync(term, warehouseId, 20, cancellationToken);

    /// <inheritdoc />
    public Task<ItemStockSnapshotDto?> ResolveBarcodeAsync(
        string barcode,
        int warehouseId,
        CancellationToken cancellationToken = default)
        => _unitOfWork.Items.GetByBarcodeAsync(barcode, warehouseId, cancellationToken);

    /// <inheritdoc />
    public async Task<bool> IsItemCodeAvailableAsync(
        string code,
        int excludeId = 0,
        CancellationToken cancellationToken = default)
        => !await _unitOfWork.Items.IsItemCodeTakenAsync(code, excludeId, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<FileExportResult> ExportAsync(
        ExportFormat format,
        int? categoryId = null,
        CancellationToken cancellationToken = default)
    {
        // One large page rather than a second "fetch everything" procedure.
        var page = await _unitOfWork.Items.GetPagedItemsAsync(
            new PagedRequest { PageNumber = 1, PageSize = AppConstants.MaxPageSize },
            categoryId,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var definition = Exports.For<ItemListDto>()
            .WithTitle("Item Master")
            .WithSubTitle($"{page.TotalCount:N0} item(s)")
            .Column("Item Code", i => i.ItemCode, width: 0.9f)
            .Column("Item Name", i => i.ItemName, width: 2.4f)
            .Column("Part Number", i => i.PartNumber, width: 1.1f)
            .Column("Category", i => i.CategoryName, width: 1.2f)
            .Column("Type", i => i.ItemTypeName, width: 0.9f)
            .Column("Unit", i => i.UnitSymbol, width: 0.5f)
            .QuantityColumn("Stock", i => i.CurrentStock)
            .QuantityColumn("Reorder Level", i => i.ReorderLevel, summable: false)
            .MoneyColumn("Avg. Cost", i => i.AverageCost, summable: false)
            .MoneyColumn("Stock Value", i => i.StockValue)
            .Column("Status", i => i.IsActive ? "Active" : "Inactive", width: 0.6f)
            .Build(page.Items);

        await _audit.LogAsync(
            AuditAction.Export, "Item", description: $"Exported the item master as {format}.",
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return _export.Export(definition, format);
    }

    /// <inheritdoc />
    public FileExportResult BuildImportTemplate()
        => _export.BuildImportTemplate(
            "Items",
            new[]
            {
                new ImportTemplateColumn(ColItemName, true, "Ball Bearing 6204ZZ", "Unique, descriptive item name."),
                new ImportTemplateColumn(ColCategory, true, "CAT-0003", "Must match an existing category code."),
                new ImportTemplateColumn(ColUnit, true, "UOM-0001", "Must match an existing unit code."),
                new ImportTemplateColumn(ColPartNumber, false, "6204ZZ", "Manufacturer part number."),
                new ImportTemplateColumn(ColManufacturer, false, "MFR-0002", "Must match an existing manufacturer code."),
                new ImportTemplateColumn(ColItemType, false, "Consumable",
                    "One of: Spare Part, Consumable, Tool, Asset, Raw Material. Defaults to Consumable."),
                new ImportTemplateColumn(ColBarcode, false, "8901234567890", "Must be unique when supplied."),
                new ImportTemplateColumn(ColHsn, false, "84821011", "Tax classification code."),
                new ImportTemplateColumn(ColReorderLevel, false, "10", "Numeric; triggers the low-stock alert."),
                new ImportTemplateColumn(ColReorderQty, false, "50", "Numeric."),
                new ImportTemplateColumn(ColMinStock, false, "5", "Numeric."),
                new ImportTemplateColumn(ColMaxStock, false, "200", "Numeric; must exceed the minimum."),
                new ImportTemplateColumn(ColStandardCost, false, "145.50", "Numeric, up to 4 decimals."),
                new ImportTemplateColumn(ColTaxRate, false, "18", "Percentage between 0 and 100."),
                new ImportTemplateColumn(ColShelf, false, "A-12-3", "Default storage location."),
                new ImportTemplateColumn(ColDescription, false, "Deep groove sealed bearing", "Free text.")
            },
            new[]
            {
                "Category, unit and manufacturer are matched on their CODE, not their name.",
                "Item codes are generated by the system; do not supply them.",
                "Rows that fail validation are reported and skipped; valid rows are still imported.",
                "Run the wizard in Validate mode first to see the errors without changing any data."
            });

    /// <inheritdoc />
    public async Task<ImportResultDto> ImportAsync(
        Stream content,
        string fileName,
        bool commit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var result = new ImportResultDto
        {
            FileName = fileName,
            IsValidationOnly = !commit
        };

        var workbook = _importReader.Read(content, "Items", AppConstants.MaxImportRows);

        var missing = workbook.MissingColumns(new[] { ColItemName, ColCategory, ColUnit });

        if (missing.Count > 0)
        {
            result.Rows.Add(new ImportRowResultDto
            {
                RowNumber = 1,
                IsValid = false,
                Errors = { $"The file is missing required column(s): {string.Join(", ", missing)}." }
            });

            result.TotalRows = 0;
            result.FailedRows = 1;
            return result;
        }

        // Resolve the lookups once instead of per row.
        var categories = (await _unitOfWork.Categories.GetLookupAsync(cancellationToken).ConfigureAwait(false))
            .ToDictionary(c => c.Code ?? string.Empty, c => c.Id, StringComparer.OrdinalIgnoreCase);
        var units = (await _unitOfWork.Units.GetLookupAsync(cancellationToken).ConfigureAwait(false))
            .ToDictionary(u => u.Code ?? string.Empty, u => u.Id, StringComparer.OrdinalIgnoreCase);
        var manufacturers = (await _unitOfWork.Manufacturers.GetLookupAsync(cancellationToken).ConfigureAwait(false))
            .ToDictionary(m => m.Code ?? string.Empty, m => m.Id, StringComparer.OrdinalIgnoreCase);

        var existingNames = await _unitOfWork.Items.Query()
            .Select(i => i.ItemName)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var seenNames = new HashSet<string>(existingNames, StringComparer.OrdinalIgnoreCase);
        var staged = new List<(ImportRowResultDto Row, ItemDto Item)>();

        result.TotalRows = workbook.Rows.Count;

        foreach (var row in workbook.Rows)
        {
            var rowResult = new ImportRowResultDto
            {
                RowNumber = row.RowNumber,
                Key = row.Text(ColItemName),
                IsValid = true
            };

            var name = row.Text(ColItemName);

            if (name.IsBlank())
            {
                rowResult.Errors.Add($"'{ColItemName}' is required.");
            }
            else if (!seenNames.Add(name!))
            {
                rowResult.IsDuplicate = true;
                rowResult.Errors.Add($"An item named '{name}' already exists.");
            }

            var categoryCode = row.Text(ColCategory);

            if (categoryCode.IsBlank() || !categories.TryGetValue(categoryCode!, out var categoryId))
            {
                rowResult.Errors.Add($"Unknown category code '{categoryCode}'.");
                categoryId = 0;
            }

            var unitCode = row.Text(ColUnit);

            if (unitCode.IsBlank() || !units.TryGetValue(unitCode!, out var unitId))
            {
                rowResult.Errors.Add($"Unknown unit code '{unitCode}'.");
                unitId = 0;
            }

            int? manufacturerId = null;
            var manufacturerCode = row.Text(ColManufacturer);

            if (!manufacturerCode.IsBlank())
            {
                if (manufacturers.TryGetValue(manufacturerCode!, out var resolved))
                {
                    manufacturerId = resolved;
                }
                else
                {
                    rowResult.Errors.Add($"Unknown manufacturer code '{manufacturerCode}'.");
                }
            }

            var itemType = ParseItemType(row.Text(ColItemType));

            if (itemType is null)
            {
                rowResult.Errors.Add($"'{row.Text(ColItemType)}' is not a valid item type.");
            }

            var minStock = row.Decimal(ColMinStock) ?? 0m;
            var maxStock = row.Decimal(ColMaxStock) ?? 0m;

            if (maxStock > 0 && maxStock < minStock)
            {
                rowResult.Errors.Add("Maximum stock cannot be less than minimum stock.");
            }

            var taxRate = row.Decimal(ColTaxRate);

            if (taxRate is < 0 or > 100)
            {
                rowResult.Errors.Add("Tax rate must be between 0 and 100.");
            }

            rowResult.IsValid = rowResult.Errors.Count == 0;
            result.Rows.Add(rowResult);

            if (!rowResult.IsValid)
            {
                continue;
            }

            staged.Add((rowResult, new ItemDto
            {
                ItemName = name!,
                PartNumber = row.Text(ColPartNumber),
                HsnCode = row.Text(ColHsn),
                Description = row.Text(ColDescription),
                CategoryId = categoryId,
                UnitId = unitId,
                ManufacturerId = manufacturerId,
                ItemType = itemType!.Value,
                Barcode = row.Text(ColBarcode),
                ReorderLevel = row.Decimal(ColReorderLevel) ?? 0m,
                ReorderQuantity = row.Decimal(ColReorderQty) ?? 0m,
                MinimumStock = minStock,
                MaximumStock = maxStock,
                StandardCost = row.Decimal(ColStandardCost) ?? 0m,
                TaxRate = taxRate,
                ShelfLocation = row.Text(ColShelf),
                IsActive = true
            }));
        }

        result.ValidRows = staged.Count;
        result.DuplicateRows = result.Rows.Count(r => r.IsDuplicate);
        result.FailedRows = result.Rows.Count(r => !r.IsValid);

        if (!commit)
        {
            return result;
        }

        // Commit the valid rows in one transaction so a failure part-way
        // through cannot leave the master half-imported.
        await _unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            foreach (var (rowResult, item) in staged)
            {
                var insert = await _unitOfWork.Items
                    .InsertItemAsync(item, _currentUser.UserId, cancellationToken)
                    .ConfigureAwait(false);

                if (insert.IsSuccess)
                {
                    result.ImportedRows++;
                }
                else
                {
                    rowResult.IsValid = false;
                    rowResult.Errors.Add(insert.Message);
                    result.FailedRows++;
                }
            }

            await _unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogError(ex, "Item import failed and was rolled back.");
            throw;
        }

        _cache.Remove(CacheKeys.Items);

        await _audit.LogAsync(
            AuditAction.Import,
            "Item",
            description: $"Imported {result.ImportedRows} item(s) from '{fileName}'; " +
                         $"{result.FailedRows} row(s) failed.",
            isSuccessful: result.FailedRows == 0,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return result;
    }

    // -----------------------------------------------------------------------
    // Business rules
    // -----------------------------------------------------------------------

    private async Task<ServiceResult> ValidateAsync(ItemDto dto, bool isUpdate, CancellationToken cancellationToken)
    {
        var errors = new List<string>();

        if (dto.ItemName.IsBlank())
        {
            errors.Add("Item name is required.");
        }

        if (dto.CategoryId <= 0)
        {
            errors.Add("Select a category.");
        }

        if (dto.UnitId <= 0)
        {
            errors.Add("Select a unit of measure.");
        }

        if (dto.MaximumStock > 0 && dto.MaximumStock < dto.MinimumStock)
        {
            errors.Add("Maximum stock cannot be less than minimum stock.");
        }

        if (dto.ReorderLevel < 0 || dto.MinimumStock < 0 || dto.StandardCost < 0)
        {
            errors.Add("Quantities and costs cannot be negative.");
        }

        if (!dto.ItemCode.IsBlank()
            && !await IsItemCodeAvailableAsync(dto.ItemCode!, isUpdate ? dto.Id : 0, cancellationToken)
                .ConfigureAwait(false))
        {
            errors.Add($"The item code '{dto.ItemCode}' is already in use.");
        }

        if (!dto.Barcode.IsBlank())
        {
            var normalized = dto.Barcode!.Trim();
            var excludeId = isUpdate ? dto.Id : 0;

            var barcodeTaken = await _unitOfWork.Items
                .AnyAsync(i => !i.IsDeleted && i.Barcode == normalized && i.Id != excludeId, cancellationToken)
                .ConfigureAwait(false);

            if (barcodeTaken)
            {
                errors.Add($"The barcode '{normalized}' is already assigned to another item.");
            }
        }

        return errors.Count == 0 ? ServiceResult.Success() : ServiceResult.ValidationFailure(errors);
    }

    /// <summary>Parses the free-text item type from the import file.</summary>
    private static ItemType? ParseItemType(string? text)
    {
        if (text.IsBlank())
        {
            return ItemType.Consumable;
        }

        var normalized = text!.Replace(" ", string.Empty, StringComparison.Ordinal);

        return Enum.TryParse<ItemType>(normalized, ignoreCase: true, out var parsed) ? parsed : null;
    }

    private static string Serialize(ItemDto dto)
        => System.Text.Json.JsonSerializer.Serialize(dto, new System.Text.Json.JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });
}
