using AutoMapper;
using Inventory.BLL.Interfaces;
using Inventory.Common.Constants;
using Inventory.Common.Enums;
using Inventory.Common.Extensions;
using Inventory.Common.Models;
using Inventory.Common.Security;
using Inventory.Entities.Dtos;
using Inventory.Entities.Models;
using Inventory.Repository.UnitOfWork;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Inventory.BLL.Services;

/// <summary>
/// Application user administration.
/// <para>
/// Identity owns password hashing, lockout and role membership, so this service
/// never touches those columns directly: it composes <see cref="UserManager{TUser}"/>
/// calls and adds the organisational rules — an account is deactivated rather
/// than deleted, the last administrator cannot be removed, and every change is
/// audited.
/// </para>
/// </summary>
public sealed class UserService : IUserService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _audit;
    private readonly IMapper _mapper;
    private readonly ILogger<UserService> _logger;

    public UserService(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditService audit,
        IMapper mapper,
        ILogger<UserService> logger)
    {
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<PagedResult<UserDto>> GetPagedAsync(
        PagedRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = _userManager.Users
            .AsNoTracking()
            .Include(u => u.Department)
            .Include(u => u.Site)
            .Where(u => !u.IsDeleted);

        if (!request.SearchTerm.IsBlank())
        {
            var term = request.SearchTerm!.Trim();
            query = query.Where(u =>
                u.UserName!.Contains(term)
                || u.FullName.Contains(term)
                || (u.Email != null && u.Email.Contains(term))
                || (u.EmployeeCode != null && u.EmployeeCode.Contains(term)));
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var users = await query
            .OrderBy(u => u.FullName)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var dtos = new List<UserDto>(users.Count);

        foreach (var user in users)
        {
            var dto = _mapper.Map<UserDto>(user);
            dto.Roles = (await _userManager.GetRolesAsync(user).ConfigureAwait(false)).ToList();
            dtos.Add(dto);
        }

        return new PagedResult<UserDto>(dtos, total, request.PageNumber, request.PageSize);
    }

    /// <inheritdoc />
    public async Task<UserDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.Users
            .AsNoTracking()
            .Include(u => u.Department)
            .Include(u => u.Site)
            .FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted, cancellationToken)
            .ConfigureAwait(false);

        if (user is null)
        {
            return null;
        }

        var dto = _mapper.Map<UserDto>(user);
        dto.Roles = (await _userManager.GetRolesAsync(user).ConfigureAwait(false)).ToList();

        return dto;
    }

    /// <inheritdoc />
    public async Task<ServiceResult<int>> CreateAsync(UserDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var errors = ValidateCommon(dto);

        if (dto.Password.IsBlank())
        {
            errors.Add("A password is required when creating a user.");
        }

        if (errors.Count > 0)
        {
            return ServiceResult<int>.From(ServiceResult.ValidationFailure(errors));
        }

        if (await _userManager.FindByNameAsync(dto.UserName).ConfigureAwait(false) is not null)
        {
            return ServiceResult<int>.Failure($"The user name '{dto.UserName}' is already taken.", -409);
        }

        if (await _userManager.FindByEmailAsync(dto.Email).ConfigureAwait(false) is not null)
        {
            return ServiceResult<int>.Failure($"The e-mail '{dto.Email}' is already registered.", -409);
        }

        var user = new ApplicationUser
        {
            UserName = dto.UserName.Trim(),
            Email = dto.Email.Trim(),
            EmailConfirmed = true,
            PhoneNumber = dto.PhoneNumber,
            FullName = dto.FullName.Trim(),
            EmployeeCode = dto.EmployeeCode.NormalizeOrNull(),
            Designation = dto.Designation.NormalizeOrNull(),
            DepartmentId = dto.DepartmentId,
            SiteId = dto.SiteId,
            IsActive = dto.IsActive,
            // A password chosen by an administrator must be changed by its owner.
            MustChangePassword = true,
            PasswordChangedOn = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId
        };

        var created = await _userManager.CreateAsync(user, dto.Password!).ConfigureAwait(false);

        if (!created.Succeeded)
        {
            return ServiceResult<int>.Failure(Describe(created), -400);
        }

        var roleResult = await SyncRolesAsync(user, dto.Roles).ConfigureAwait(false);

        if (!roleResult.Succeeded)
        {
            return ServiceResult<int>.From(roleResult);
        }

        await _audit.LogAsync(
            AuditAction.Create, "User", user.Id.ToString(),
            $"Created user '{user.UserName}' with role(s) {dto.Roles.JoinWith()}.",
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return ServiceResult<int>.Success(user.Id, "User created successfully.");
    }

    /// <inheritdoc />
    public async Task<ServiceResult> UpdateAsync(UserDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var user = await _userManager.FindByIdAsync(dto.Id.ToString()).ConfigureAwait(false);

        if (user is null || user.IsDeleted)
        {
            return ServiceResult.Failure("The user was not found.", -404);
        }

        var errors = ValidateCommon(dto);

        if (errors.Count > 0)
        {
            return ServiceResult.ValidationFailure(errors);
        }

        var previousRoles = await _userManager.GetRolesAsync(user).ConfigureAwait(false);

        // Refuse to strip the last administrator of its role.
        if (previousRoles.Contains(Roles.Administrator)
            && !dto.Roles.Contains(Roles.Administrator)
            && await CountAdministratorsAsync(cancellationToken).ConfigureAwait(false) <= 1)
        {
            return ServiceResult.Failure(
                "This is the only administrator account; assign the role to another user first.", -409);
        }

        if (user.IsActive && !dto.IsActive
            && previousRoles.Contains(Roles.Administrator)
            && await CountAdministratorsAsync(cancellationToken).ConfigureAwait(false) <= 1)
        {
            return ServiceResult.Failure("The only administrator account cannot be deactivated.", -409);
        }

        user.FullName = dto.FullName.Trim();
        user.Email = dto.Email.Trim();
        user.PhoneNumber = dto.PhoneNumber;
        user.EmployeeCode = dto.EmployeeCode.NormalizeOrNull();
        user.Designation = dto.Designation.NormalizeOrNull();
        user.DepartmentId = dto.DepartmentId;
        user.SiteId = dto.SiteId;
        user.IsActive = dto.IsActive;
        user.MustChangePassword = dto.MustChangePassword;
        user.ModifiedOn = DateTime.UtcNow;
        user.ModifiedBy = _currentUser.UserId;

        var updated = await _userManager.UpdateAsync(user).ConfigureAwait(false);

        if (!updated.Succeeded)
        {
            return ServiceResult.Failure(Describe(updated), -400);
        }

        var roleResult = await SyncRolesAsync(user, dto.Roles).ConfigureAwait(false);

        if (!roleResult.Succeeded)
        {
            return roleResult;
        }

        await _audit.LogAsync(
            AuditAction.Update, "User", user.Id.ToString(),
            $"Updated user '{user.UserName}'. Roles: {previousRoles.JoinWith()} to {dto.Roles.JoinWith()}.",
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return ServiceResult.Success("User updated successfully.");
    }

    /// <inheritdoc />
    public async Task<ServiceResult> DeactivateAsync(int id, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(id.ToString()).ConfigureAwait(false);

        if (user is null || user.IsDeleted)
        {
            return ServiceResult.Failure("The user was not found.", -404);
        }

        if (user.Id == _currentUser.UserId)
        {
            return ServiceResult.Failure("You cannot deactivate your own account.", -409);
        }

        if (await _userManager.IsInRoleAsync(user, Roles.Administrator).ConfigureAwait(false)
            && await CountAdministratorsAsync(cancellationToken).ConfigureAwait(false) <= 1)
        {
            return ServiceResult.Failure("The only administrator account cannot be deactivated.", -409);
        }

        // Accounts are never physically removed: audit rows, approvals and
        // documents all reference the user id.
        user.IsActive = false;
        user.IsDeleted = true;
        user.ModifiedOn = DateTime.UtcNow;
        user.ModifiedBy = _currentUser.UserId;
        user.LockoutEnd = DateTimeOffset.MaxValue;

        var result = await _userManager.UpdateAsync(user).ConfigureAwait(false);

        if (!result.Succeeded)
        {
            return ServiceResult.Failure(Describe(result), -400);
        }

        await _audit.LogAsync(
            AuditAction.Delete, "User", id.ToString(),
            $"Deactivated user '{user.UserName}'.",
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return ServiceResult.Success("User deactivated.");
    }

    /// <inheritdoc />
    public async Task<ServiceResult> ResetPasswordAsync(
        int id,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(id.ToString()).ConfigureAwait(false);

        if (user is null || user.IsDeleted)
        {
            return ServiceResult.Failure("The user was not found.", -404);
        }

        if (newPassword.IsBlank())
        {
            return ServiceResult.Failure("Enter a new password.", -400);
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user).ConfigureAwait(false);
        var result = await _userManager.ResetPasswordAsync(user, token, newPassword).ConfigureAwait(false);

        if (!result.Succeeded)
        {
            return ServiceResult.Failure(Describe(result), -400);
        }

        user.MustChangePassword = true;
        user.PasswordChangedOn = DateTime.UtcNow;
        await _userManager.UpdateAsync(user).ConfigureAwait(false);

        await _audit.LogAsync(
            AuditAction.PasswordChange, "User", id.ToString(),
            $"Administrator reset the password for '{user.UserName}'.",
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return ServiceResult.Success("Password reset. The user must change it at the next sign-in.");
    }

    /// <inheritdoc />
    public async Task<ServiceResult> UnlockAsync(int id, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(id.ToString()).ConfigureAwait(false);

        if (user is null || user.IsDeleted)
        {
            return ServiceResult.Failure("The user was not found.", -404);
        }

        await _userManager.SetLockoutEndDateAsync(user, null).ConfigureAwait(false);
        await _userManager.ResetAccessFailedCountAsync(user).ConfigureAwait(false);

        await _audit.LogAsync(
            AuditAction.Update, "User", id.ToString(),
            $"Unlocked user '{user.UserName}'.",
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return ServiceResult.Success("User unlocked.");
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetRolesAsync(CancellationToken cancellationToken = default)
        => await _roleManager.Roles
            .AsNoTracking()
            .Where(r => r.Name != null)
            .OrderBy(r => r.Name)
            .Select(r => r.Name!)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<LookupDto>> GetApproverLookupAsync(CancellationToken cancellationToken = default)
    {
        var approvers = await _userManager.GetUsersInRoleAsync(Roles.Approver).ConfigureAwait(false);

        return approvers
            .Where(u => u.IsActive && !u.IsDeleted)
            .OrderBy(u => u.FullName)
            .Select(u => new LookupDto { Id = u.Id, Text = u.FullName, Code = u.EmployeeCode })
            .ToList();
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static List<string> ValidateCommon(UserDto dto)
    {
        var errors = new List<string>();

        if (dto.UserName.IsBlank())
        {
            errors.Add("User name is required.");
        }

        if (dto.FullName.IsBlank())
        {
            errors.Add("Full name is required.");
        }

        if (dto.Email.IsBlank())
        {
            errors.Add("E-mail is required.");
        }

        if (dto.Roles.Count == 0)
        {
            errors.Add("Assign at least one role.");
        }

        var unknown = dto.Roles.Where(r => !Roles.All.Contains(r, StringComparer.OrdinalIgnoreCase)).ToList();

        if (unknown.Count > 0)
        {
            errors.Add($"Unknown role(s): {unknown.JoinWith()}.");
        }

        if (!dto.Password.IsBlank() && dto.Password != dto.ConfirmPassword)
        {
            errors.Add("The passwords do not match.");
        }

        return errors;
    }

    /// <summary>Adds and removes roles so the user ends up with exactly <paramref name="desired"/>.</summary>
    private async Task<ServiceResult> SyncRolesAsync(ApplicationUser user, IReadOnlyCollection<string> desired)
    {
        var current = await _userManager.GetRolesAsync(user).ConfigureAwait(false);

        var toRemove = current.Except(desired, StringComparer.OrdinalIgnoreCase).ToList();
        var toAdd = desired.Except(current, StringComparer.OrdinalIgnoreCase).ToList();

        if (toRemove.Count > 0)
        {
            var removed = await _userManager.RemoveFromRolesAsync(user, toRemove).ConfigureAwait(false);

            if (!removed.Succeeded)
            {
                return ServiceResult.Failure(Describe(removed), -400);
            }
        }

        if (toAdd.Count > 0)
        {
            var added = await _userManager.AddToRolesAsync(user, toAdd).ConfigureAwait(false);

            if (!added.Succeeded)
            {
                return ServiceResult.Failure(Describe(added), -400);
            }
        }

        return ServiceResult.Success();
    }

    private async Task<int> CountAdministratorsAsync(CancellationToken cancellationToken)
    {
        var admins = await _userManager.GetUsersInRoleAsync(Roles.Administrator).ConfigureAwait(false);
        return admins.Count(u => u.IsActive && !u.IsDeleted);
    }

    private static string Describe(IdentityResult result)
        => string.Join(" ", result.Errors.Select(e => e.Description));
}
