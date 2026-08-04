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

namespace Inventory.BLL.Services;

/// <summary>
/// Vendor master business service. The vendor row carries statutory identifiers
/// (GSTIN, PAN, IFSC) whose formats are validated here as well as by the DTO
/// annotations, because imports and API callers bypass model binding.
/// </summary>
public sealed class VendorService : IVendorService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _audit;
    private readonly IExportService _export;
    private readonly ICacheService _cache;

    public VendorService(
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditService audit,
        IExportService export,
        ICacheService cache)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
        _export = export ?? throw new ArgumentNullException(nameof(export));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    /// <inheritdoc />
    public Task<PagedResult<VendorDto>> GetPagedAsync(
        PagedRequest request,
        bool? blacklistedOnly = null,
        CancellationToken cancellationToken = default)
        => _unitOfWork.Vendors.GetPagedVendorsAsync(request, blacklistedOnly, cancellationToken);

    /// <inheritdoc />
    public Task<VendorDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => _unitOfWork.Vendors.GetVendorAsync(id, cancellationToken);

    /// <inheritdoc />
    public async Task<ServiceResult<int>> CreateAsync(VendorDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var validation = await ValidateAsync(dto, isUpdate: false, cancellationToken).ConfigureAwait(false);

        if (!validation.Succeeded)
        {
            return ServiceResult<int>.From(validation);
        }

        if (dto.Code.IsBlank())
        {
            dto.Code = await _unitOfWork.Vendors.GenerateCodeAsync("VEN", cancellationToken).ConfigureAwait(false);
        }

        var result = await _unitOfWork.Vendors
            .InsertVendorAsync(dto, _currentUser.UserId, cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return ServiceResult<int>.Failure(result.Message, result.ReturnCode);
        }

        _cache.Remove(CacheKeys.Vendors);

        await _audit.LogAsync(
            AuditAction.Create, "Vendor", result.NewId?.ToString(),
            $"Created vendor '{dto.Name}' ({dto.Code}).",
            newValues: Serialize(dto),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return ServiceResult<int>.Success(result.NewId ?? 0, "Vendor created successfully.");
    }

    /// <inheritdoc />
    public async Task<ServiceResult> UpdateAsync(VendorDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var existing = await _unitOfWork.Vendors.GetVendorAsync(dto.Id, cancellationToken).ConfigureAwait(false);

        if (existing is null)
        {
            return ServiceResult.Failure("The vendor was not found.", -404);
        }

        var validation = await ValidateAsync(dto, isUpdate: true, cancellationToken).ConfigureAwait(false);

        if (!validation.Succeeded)
        {
            return validation;
        }

        var result = await _unitOfWork.Vendors
            .UpdateVendorAsync(dto, _currentUser.UserId, cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return ServiceResult.Failure(result.Message, result.ReturnCode);
        }

        _cache.Remove(CacheKeys.Vendors);

        await _audit.LogAsync(
            AuditAction.Update, "Vendor", dto.Id.ToString(),
            $"Updated vendor '{dto.Name}'.",
            oldValues: Serialize(existing),
            newValues: Serialize(dto),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return ServiceResult.Success("Vendor updated successfully.");
    }

    /// <inheritdoc />
    public async Task<ServiceResult> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var existing = await _unitOfWork.Vendors.GetVendorAsync(id, cancellationToken).ConfigureAwait(false);

        if (existing is null)
        {
            return ServiceResult.Failure("The vendor was not found.", -404);
        }

        var result = await _unitOfWork.Vendors
            .DeleteVendorAsync(id, _currentUser.UserId, cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return ServiceResult.Failure(result.Message, result.ReturnCode);
        }

        _cache.Remove(CacheKeys.Vendors);

        await _audit.LogAsync(
            AuditAction.Delete, "Vendor", id.ToString(),
            $"Deleted vendor '{existing.Name}'.",
            oldValues: Serialize(existing),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return ServiceResult.Success("Vendor deleted successfully.");
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<LookupDto>> GetLookupAsync(CancellationToken cancellationToken = default)
        => _unitOfWork.Vendors.GetLookupAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<bool> IsCodeAvailableAsync(
        string code,
        int excludeId = 0,
        CancellationToken cancellationToken = default)
    {
        if (code.IsBlank())
        {
            return true;
        }

        var normalized = code.Trim();

        return !await _unitOfWork.Vendors
            .AnyAsync(v => !v.IsDeleted && v.Code == normalized && v.Id != excludeId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> IsGstAvailableAsync(
        string gst,
        int excludeId = 0,
        CancellationToken cancellationToken = default)
        => !await _unitOfWork.Vendors.IsGstNumberTakenAsync(gst, excludeId, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<FileExportResult> ExportAsync(
        ExportFormat format,
        CancellationToken cancellationToken = default)
    {
        var page = await _unitOfWork.Vendors.GetPagedVendorsAsync(
            new PagedRequest { PageNumber = 1, PageSize = AppConstants.MaxPageSize },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var definition = Exports.For<VendorDto>()
            .WithTitle("Vendor Master")
            .WithSubTitle($"{page.TotalCount:N0} vendor(s)")
            .Column("Code", v => v.Code, width: 0.7f)
            .Column("Vendor Name", v => v.Name, width: 2.2f)
            .Column("Contact Person", v => v.ContactPerson, width: 1.3f)
            .Column("Contact Number", v => v.ContactNumber, width: 1f)
            .Column("City", v => v.City, width: 0.9f)
            .Column("GSTIN", v => v.GstNumber, width: 1.2f)
            .Column("Credit Days", v => v.CreditDays, alignRight: true)
            .Column("Receipts", v => v.TotalReceipts, alignRight: true, summable: true)
            .MoneyColumn("Purchase Value", v => v.TotalPurchaseValue)
            .Column("Status", v => v.IsBlacklisted ? "Blacklisted" : v.IsActive ? "Active" : "Inactive", width: 0.8f)
            .Build(page.Items);

        await _audit.LogAsync(
            AuditAction.Export, "Vendor", description: $"Exported the vendor master as {format}.",
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return _export.Export(definition, format);
    }

    // -----------------------------------------------------------------------
    // Business rules
    // -----------------------------------------------------------------------

    private async Task<ServiceResult> ValidateAsync(VendorDto dto, bool isUpdate, CancellationToken cancellationToken)
    {
        var errors = new List<string>();

        if (dto.Name.IsBlank())
        {
            errors.Add("Vendor name is required.");
        }

        if (!dto.Code.IsBlank()
            && !await IsCodeAvailableAsync(dto.Code, isUpdate ? dto.Id : 0, cancellationToken).ConfigureAwait(false))
        {
            errors.Add($"The vendor code '{dto.Code}' is already in use.");
        }

        if (!dto.GstNumber.IsBlank())
        {
            var gst = dto.GstNumber!.Trim().ToUpperInvariant();

            if (gst.Length != 15)
            {
                errors.Add("GSTIN must be exactly 15 characters.");
            }
            else if (!await IsGstAvailableAsync(gst, isUpdate ? dto.Id : 0, cancellationToken).ConfigureAwait(false))
            {
                errors.Add($"GSTIN '{gst}' is already registered against another vendor.");
            }
            else if (!dto.PanNumber.IsBlank()
                     && !gst.Substring(2, 10).Equals(dto.PanNumber!.Trim().ToUpperInvariant(), StringComparison.Ordinal))
            {
                // Characters 3-12 of a GSTIN are the holder's PAN.
                errors.Add("The PAN does not match the PAN embedded in the GSTIN.");
            }

            dto.GstNumber = gst;
        }

        if (!dto.PanNumber.IsBlank())
        {
            dto.PanNumber = dto.PanNumber!.Trim().ToUpperInvariant();

            if (dto.PanNumber.Length != 10)
            {
                errors.Add("PAN must be exactly 10 characters.");
            }
        }

        if (!dto.IfscCode.IsBlank())
        {
            dto.IfscCode = dto.IfscCode!.Trim().ToUpperInvariant();

            if (dto.IfscCode.Length != 11)
            {
                errors.Add("IFSC code must be exactly 11 characters.");
            }
        }

        if (dto.CreditDays is < 0 or > 365)
        {
            errors.Add("Credit days must be between 0 and 365.");
        }

        if (dto.Rating is < 1 or > 5)
        {
            errors.Add("Rating must be between 1 and 5.");
        }

        return errors.Count == 0 ? ServiceResult.Success() : ServiceResult.ValidationFailure(errors);
    }

    private static string Serialize(VendorDto dto)
        => System.Text.Json.JsonSerializer.Serialize(dto, new System.Text.Json.JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });
}
