using AutoMapper;
using Inventory.Common.Extensions;
using Inventory.Entities.Dtos;
using Inventory.Entities.Models;

namespace Inventory.BLL.Mapping;

/// <summary>
/// Entity to DTO mapping. Only mappings that are genuinely mechanical live here;
/// anything that needs a database join (stock balances, aggregates) is produced
/// by the stored procedures instead, so the mapper never triggers lazy loads.
/// </summary>
public sealed class MappingProfile : Profile
{
    public MappingProfile()
    {
        ConfigureMasters();
        ConfigureItems();
        ConfigureDocuments();
        ConfigureSystem();
    }

    private void ConfigureMasters()
    {
        // Bidirectional so the same profile serves reads and SP parameter building.
        CreateMap<Category, CategoryDto>()
            .ForMember(d => d.ParentCategoryName, o => o.MapFrom(s => s.ParentCategory != null ? s.ParentCategory.Name : null))
            .ForMember(d => d.ItemCount, o => o.Ignore())
            .ForMember(d => d.CreatedByName, o => o.Ignore())
            .ReverseMap()
            .ForMember(d => d.ParentCategory, o => o.Ignore())
            .ForMember(d => d.SubCategories, o => o.Ignore())
            .ForMember(d => d.Items, o => o.Ignore())
            .ForMember(d => d.RowVersion, o => o.Ignore());

        CreateMap<Unit, UnitDto>()
            .ForMember(d => d.CreatedByName, o => o.Ignore())
            .ReverseMap()
            .ForMember(d => d.Items, o => o.Ignore())
            .ForMember(d => d.RowVersion, o => o.Ignore());

        CreateMap<Department, DepartmentDto>()
            .ForMember(d => d.CreatedByName, o => o.Ignore())
            .ReverseMap()
            .ForMember(d => d.Engineers, o => o.Ignore())
            .ForMember(d => d.Issues, o => o.Ignore())
            .ForMember(d => d.RowVersion, o => o.Ignore());

        CreateMap<Site, SiteDto>()
            .ForMember(d => d.WarehouseCount, o => o.MapFrom(s => s.Warehouses.Count))
            .ForMember(d => d.CreatedByName, o => o.Ignore())
            .ReverseMap()
            .ForMember(d => d.Warehouses, o => o.Ignore())
            .ForMember(d => d.RowVersion, o => o.Ignore());

        CreateMap<Warehouse, WarehouseDto>()
            .ForMember(d => d.SiteName, o => o.MapFrom(s => s.Site != null ? s.Site.Name : null))
            .ForMember(d => d.CreatedByName, o => o.Ignore())
            .ReverseMap()
            .ForMember(d => d.Site, o => o.Ignore())
            .ForMember(d => d.LedgerEntries, o => o.Ignore())
            .ForMember(d => d.RowVersion, o => o.Ignore());

        CreateMap<Manufacturer, ManufacturerDto>()
            .ForMember(d => d.CreatedByName, o => o.Ignore())
            .ReverseMap()
            .ForMember(d => d.Items, o => o.Ignore())
            .ForMember(d => d.RowVersion, o => o.Ignore());

        CreateMap<Courier, CourierDto>()
            .ForMember(d => d.CreatedByName, o => o.Ignore())
            .ReverseMap()
            .ForMember(d => d.RowVersion, o => o.Ignore());

        CreateMap<Engineer, EngineerDto>()
            .ForMember(d => d.DepartmentName, o => o.MapFrom(s => s.Department != null ? s.Department.Name : null))
            .ForMember(d => d.SiteName, o => o.MapFrom(s => s.Site != null ? s.Site.Name : null))
            .ForMember(d => d.CreatedByName, o => o.Ignore())
            .ReverseMap()
            .ForMember(d => d.Department, o => o.Ignore())
            .ForMember(d => d.Site, o => o.Ignore())
            .ForMember(d => d.Issues, o => o.Ignore())
            .ForMember(d => d.RowVersion, o => o.Ignore());

        CreateMap<Vendor, VendorDto>()
            .ForMember(d => d.TotalReceipts, o => o.Ignore())
            .ForMember(d => d.TotalPurchaseValue, o => o.Ignore())
            .ForMember(d => d.LastSupplyDate, o => o.Ignore())
            .ForMember(d => d.CreatedByName, o => o.Ignore())
            .ReverseMap()
            .ForMember(d => d.Receipts, o => o.Ignore())
            .ForMember(d => d.RowVersion, o => o.Ignore());

        // Every master projects to the shared lookup shape.
        CreateMap<Category, LookupDto>().ConvertUsing(MapLookup);
        CreateMap<Unit, LookupDto>().ConvertUsing(MapLookup);
        CreateMap<Department, LookupDto>().ConvertUsing(MapLookup);
        CreateMap<Site, LookupDto>().ConvertUsing(MapLookup);
        CreateMap<Manufacturer, LookupDto>().ConvertUsing(MapLookup);
        CreateMap<Courier, LookupDto>().ConvertUsing(MapLookup);
        CreateMap<Vendor, LookupDto>().ConvertUsing(MapLookup);
    }

    private void ConfigureItems()
    {
        CreateMap<Item, ItemDto>()
            .ForMember(d => d.CategoryName, o => o.MapFrom(s => s.Category != null ? s.Category.Name : null))
            .ForMember(d => d.UnitName, o => o.MapFrom(s => s.Unit != null ? s.Unit.Name : null))
            .ForMember(d => d.UnitSymbol, o => o.MapFrom(s => s.Unit != null ? s.Unit.Symbol : null))
            .ForMember(d => d.ManufacturerName, o => o.MapFrom(s => s.Manufacturer != null ? s.Manufacturer.Name : null))
            .ForMember(d => d.ItemTypeName, o => o.MapFrom(s => s.ItemType.ToDisplayName()))
            // Balance and value come from the ledger, never from the item row.
            .ForMember(d => d.CurrentStock, o => o.Ignore())
            .ForMember(d => d.StockValue, o => o.Ignore())
            .ForMember(d => d.PrimaryImagePath, o => o.Ignore());

        CreateMap<ItemDto, Item>()
            .ForMember(d => d.Category, o => o.Ignore())
            .ForMember(d => d.Unit, o => o.Ignore())
            .ForMember(d => d.Manufacturer, o => o.Ignore())
            .ForMember(d => d.Images, o => o.Ignore())
            .ForMember(d => d.LedgerEntries, o => o.Ignore())
            .ForMember(d => d.ReceiptLines, o => o.Ignore())
            .ForMember(d => d.IssueLines, o => o.Ignore())
            .ForMember(d => d.AverageCost, o => o.Ignore())
            .ForMember(d => d.RowVersion, o => o.Ignore())
            .ForMember(d => d.ItemCode, o => o.Condition(s => !string.IsNullOrWhiteSpace(s.ItemCode)));

        CreateMap<Item, ItemListDto>()
            .ForMember(d => d.CategoryName, o => o.MapFrom(s => s.Category != null ? s.Category.Name : null))
            .ForMember(d => d.UnitSymbol, o => o.MapFrom(s => s.Unit != null ? s.Unit.Symbol : null))
            .ForMember(d => d.ManufacturerName, o => o.MapFrom(s => s.Manufacturer != null ? s.Manufacturer.Name : null))
            .ForMember(d => d.ItemTypeName, o => o.MapFrom(s => s.ItemType.ToDisplayName()))
            .ForMember(d => d.CurrentStock, o => o.Ignore())
            .ForMember(d => d.StockValue, o => o.Ignore())
            .ForMember(d => d.IsLowStock, o => o.Ignore());

        CreateMap<Item, LookupDto>()
            .ForMember(d => d.Id, o => o.MapFrom(s => s.Id))
            .ForMember(d => d.Text, o => o.MapFrom(s => s.ItemName))
            .ForMember(d => d.Code, o => o.MapFrom(s => s.ItemCode))
            .ForMember(d => d.ParentId, o => o.MapFrom(s => (int?)s.CategoryId))
            .ForMember(d => d.Extra, o => o.MapFrom(s => s.PartNumber));
    }

    private void ConfigureDocuments()
    {
        CreateMap<InventoryInwardHeader, GrnDto>()
            .ForMember(d => d.VendorName, o => o.MapFrom(s => s.Vendor != null ? s.Vendor.Name : null))
            .ForMember(d => d.WarehouseName, o => o.MapFrom(s => s.Warehouse != null ? s.Warehouse.Name : null))
            .ForMember(d => d.CourierName, o => o.MapFrom(s => s.Courier != null ? s.Courier.Name : null))
            .ForMember(d => d.StatusName, o => o.MapFrom(s => s.Status.ToDisplayName()))
            .ForMember(d => d.ReceivedByName, o => o.Ignore())
            .ForMember(d => d.ApprovedByName, o => o.Ignore())
            .ForMember(d => d.Details, o => o.MapFrom(s => s.Details))
            .ForMember(d => d.Attachments, o => o.Ignore())
            .ForMember(d => d.ApprovalTrail, o => o.Ignore());

        CreateMap<InventoryInwardDetail, GrnDetailDto>()
            .ForMember(d => d.ItemCode, o => o.MapFrom(s => s.Item != null ? s.Item.ItemCode : null))
            .ForMember(d => d.ItemName, o => o.MapFrom(s => s.Item != null ? s.Item.ItemName : null))
            .ForMember(d => d.UnitSymbol, o => o.MapFrom(s => s.Item != null && s.Item.Unit != null ? s.Item.Unit.Symbol : null));

        CreateMap<InventoryOutwardHeader, IssueDto>()
            .ForMember(d => d.DepartmentName, o => o.MapFrom(s => s.Department != null ? s.Department.Name : null))
            .ForMember(d => d.SiteName, o => o.MapFrom(s => s.Site != null ? s.Site.Name : null))
            .ForMember(d => d.EngineerName, o => o.MapFrom(s => s.Engineer != null ? s.Engineer.Name : null))
            .ForMember(d => d.WarehouseName, o => o.MapFrom(s => s.Warehouse != null ? s.Warehouse.Name : null))
            .ForMember(d => d.CourierName, o => o.MapFrom(s => s.Courier != null ? s.Courier.Name : null))
            .ForMember(d => d.StatusName, o => o.MapFrom(s => s.Status.ToDisplayName()))
            .ForMember(d => d.RequestedByName, o => o.Ignore())
            .ForMember(d => d.ApprovedByName, o => o.Ignore())
            .ForMember(d => d.IssuedByName, o => o.Ignore())
            .ForMember(d => d.Details, o => o.MapFrom(s => s.Details))
            .ForMember(d => d.Attachments, o => o.Ignore())
            .ForMember(d => d.ApprovalTrail, o => o.Ignore());

        CreateMap<InventoryOutwardDetail, IssueDetailDto>()
            .ForMember(d => d.ItemCode, o => o.MapFrom(s => s.Item != null ? s.Item.ItemCode : null))
            .ForMember(d => d.ItemName, o => o.MapFrom(s => s.Item != null ? s.Item.ItemName : null))
            .ForMember(d => d.UnitSymbol, o => o.MapFrom(s => s.Item != null && s.Item.Unit != null ? s.Item.Unit.Symbol : null))
            .ForMember(d => d.AvailableStock, o => o.Ignore());

        CreateMap<ApprovalHistory, ApprovalHistoryDto>()
            .ForMember(d => d.Action, o => o.MapFrom(s => s.Action.ToDisplayName()))
            .ForMember(d => d.FromStatus, o => o.MapFrom(s => s.FromStatus.ToDisplayName()))
            .ForMember(d => d.ToStatus, o => o.MapFrom(s => s.ToStatus.ToDisplayName()));
    }

    private void ConfigureSystem()
    {
        CreateMap<Attachment, AttachmentDto>()
            .ForMember(d => d.UploadedByName, o => o.Ignore())
            .ForMember(d => d.DownloadUrl, o => o.Ignore());

        CreateMap<Notification, NotificationDto>()
            .ForMember(d => d.NotificationTypeName, o => o.MapFrom(s => s.NotificationType.ToDisplayName()));

        CreateMap<AuditLog, AuditLogDto>()
            .ForMember(d => d.ActionName, o => o.MapFrom(s => s.Action.ToDisplayName()));

        CreateMap<LoginHistory, LoginHistoryDto>()
            .ForMember(d => d.FullName, o => o.MapFrom(s => s.User != null ? s.User.FullName : null));

        CreateMap<ApplicationUser, UserDto>()
            .ForMember(d => d.DepartmentName, o => o.MapFrom(s => s.Department != null ? s.Department.Name : null))
            .ForMember(d => d.SiteName, o => o.MapFrom(s => s.Site != null ? s.Site.Name : null))
            .ForMember(d => d.Roles, o => o.Ignore())
            .ForMember(d => d.IsLockedOut, o => o.MapFrom(s => s.LockoutEnd != null && s.LockoutEnd > DateTimeOffset.UtcNow))
            .ForMember(d => d.LockoutEnd, o => o.MapFrom(s => s.LockoutEnd != null ? s.LockoutEnd.Value.UtcDateTime : (DateTime?)null))
            .ForMember(d => d.Password, o => o.Ignore())
            .ForMember(d => d.ConfirmPassword, o => o.Ignore());
    }

    /// <summary>Shared projection for the master lookup shape.</summary>
    private static LookupDto MapLookup(MasterEntity source, LookupDto destination, ResolutionContext context)
        => new()
        {
            Id = source.Id,
            Text = source.Name,
            Code = source.Code
        };
}
