using System.Security.Claims;
using Inventory.BLL.Interfaces;
using Inventory.Common.Constants;
using Inventory.Common.Enums;
using Inventory.Entities.Models;
using Inventory.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Web.Controllers;

/// <summary>
/// Authentication: sign-in, sign-out, password change and password reset.
/// <para>
/// Password verification, lockout and hashing are all handled by ASP.NET Core
/// Identity — nothing here compares a password itself. The controller adds the
/// organisational rules on top: an inactive account cannot sign in, a temporary
/// or expired password forces a change, and every attempt is recorded.
/// </para>
/// </summary>
[AllowAnonymous]
public sealed class AccountController : BaseController
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _audit;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IAuditService audit,
        ILogger<AccountController> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _audit = audit;
        _logger = logger;
    }

    // -----------------------------------------------------------------------
    // Sign in
    // -----------------------------------------------------------------------

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.FindByNameAsync(model.UserName).ConfigureAwait(false);

        // Deliberately identical messaging whether the account exists or not, so
        // the form cannot be used to enumerate valid user names.
        const string InvalidCredentials = "Invalid user name or password.";

        if (user is null || user.IsDeleted)
        {
            await RecordFailureAsync(model.UserName, "Unknown user name", cancellationToken).ConfigureAwait(false);
            ModelState.AddModelError(string.Empty, InvalidCredentials);
            return View(model);
        }

        if (!user.IsActive)
        {
            await RecordFailureAsync(model.UserName, "Account deactivated", cancellationToken).ConfigureAwait(false);
            ModelState.AddModelError(string.Empty,
                "This account has been deactivated. Please contact your administrator.");
            return View(model);
        }

        var result = await _signInManager
            .PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: true)
            .ConfigureAwait(false);

        if (result.IsLockedOut)
        {
            await RecordFailureAsync(model.UserName, "Account locked out", cancellationToken).ConfigureAwait(false);
            ModelState.AddModelError(string.Empty,
                $"This account is locked for {AppConstants.LockoutMinutes} minutes after " +
                $"{AppConstants.MaxFailedLoginAttempts} failed attempts.");
            return View(model);
        }

        if (!result.Succeeded)
        {
            await RecordFailureAsync(model.UserName, "Incorrect password", cancellationToken).ConfigureAwait(false);
            ModelState.AddModelError(string.Empty, InvalidCredentials);
            return View(model);
        }

        // Successful: stamp the extra claims the application relies on.
        await AddApplicationClaimsAsync(user).ConfigureAwait(false);

        user.LastLoginOn = DateTime.UtcNow;
        await _userManager.UpdateAsync(user).ConfigureAwait(false);

        await _audit.RecordLoginAsync(user.Id, user.UserName!, true, null, cancellationToken).ConfigureAwait(false);
        await _audit.LogAsync(AuditAction.Login, "User", user.Id.ToString(),
            $"{user.UserName} signed in.", cancellationToken: cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("User {UserName} signed in.", user.UserName);

        if (RequiresPasswordChange(user))
        {
            Warning("Your password must be changed before you continue.");
            return RedirectToAction(nameof(ChangePassword), new { required = true });
        }

        return RedirectToLocal(model.ReturnUrl);
    }

    // -----------------------------------------------------------------------
    // Sign out
    // -----------------------------------------------------------------------

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var userName = User.Identity?.Name ?? "unknown";
        var userId = _userManager.GetUserId(User);

        await _signInManager.SignOutAsync().ConfigureAwait(false);
        HttpContext.Session.Clear();

        await _audit.LogAsync(AuditAction.Logout, "User", userId,
            $"{userName} signed out.", cancellationToken: cancellationToken).ConfigureAwait(false);

        return RedirectToAction(nameof(Login));
    }

    // -----------------------------------------------------------------------
    // Change password
    // -----------------------------------------------------------------------

    [HttpGet]
    [Authorize]
    public IActionResult ChangePassword(bool required = false)
        => View(new ChangePasswordViewModel { IsRequired = required });

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.GetUserAsync(User).ConfigureAwait(false);

        if (user is null)
        {
            return RedirectToAction(nameof(Login));
        }

        if (string.Equals(model.CurrentPassword, model.NewPassword, StringComparison.Ordinal))
        {
            ModelState.AddModelError(nameof(model.NewPassword),
                "The new password must be different from the current one.");
            return View(model);
        }

        var result = await _userManager
            .ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword)
            .ConfigureAwait(false);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        user.PasswordChangedOn = DateTime.UtcNow;
        user.MustChangePassword = false;
        await _userManager.UpdateAsync(user).ConfigureAwait(false);

        // Refresh the principal so the must-change claim disappears immediately.
        await _signInManager.RefreshSignInAsync(user).ConfigureAwait(false);
        await AddApplicationClaimsAsync(user).ConfigureAwait(false);

        await _audit.LogAsync(AuditAction.PasswordChange, "User", user.Id.ToString(),
            $"{user.UserName} changed their password.", cancellationToken: cancellationToken).ConfigureAwait(false);

        Success("Your password has been changed.");
        return RedirectToAction("Index", "Dashboard");
    }

    // -----------------------------------------------------------------------
    // Forgot password
    // -----------------------------------------------------------------------

    [HttpGet]
    public IActionResult ForgotPassword() => View(new ForgotPasswordViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.FindByEmailAsync(model.Email).ConfigureAwait(false);

        // The confirmation is shown whether or not the address is registered, so
        // the form cannot be used to discover which addresses exist.
        if (user is not null && user.IsActive && !user.IsDeleted)
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user).ConfigureAwait(false);

            _logger.LogInformation(
                "Password reset requested for {Email}. Token generated (length {Length}).",
                model.Email, token.Length);

            await _audit.LogAsync(AuditAction.PasswordChange, "User", user.Id.ToString(),
                $"Password reset requested for {user.UserName}.",
                cancellationToken: cancellationToken).ConfigureAwait(false);

            // In this build the administrator resets the password from the user
            // master; wire an e-mail here once outbound mail is approved.
        }

        return View("ForgotPasswordConfirmation");
    }

    // -----------------------------------------------------------------------
    // Access denied
    // -----------------------------------------------------------------------

    [HttpGet]
    public IActionResult AccessDenied(string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Adds the claims the application reads on every request, so a screen never
    /// has to hit the database for the signed-in user's name or department.
    /// </summary>
    private async Task AddApplicationClaimsAsync(ApplicationUser user)
    {
        var existing = await _userManager.GetClaimsAsync(user).ConfigureAwait(false);

        var desired = new List<Claim>
        {
            new(AppClaimTypes.FullName, user.FullName),
            new(AppClaimTypes.MustChangePassword, RequiresPasswordChange(user) ? "true" : "false")
        };

        if (!string.IsNullOrWhiteSpace(user.EmployeeCode))
        {
            desired.Add(new Claim(AppClaimTypes.EmployeeCode, user.EmployeeCode));
        }

        if (user.DepartmentId.HasValue)
        {
            desired.Add(new Claim(AppClaimTypes.DepartmentId, user.DepartmentId.Value.ToString()));
        }

        if (user.SiteId.HasValue)
        {
            desired.Add(new Claim(AppClaimTypes.SiteId, user.SiteId.Value.ToString()));
        }

        foreach (var claim in desired)
        {
            var current = existing.FirstOrDefault(c => c.Type == claim.Type);

            if (current is null)
            {
                await _userManager.AddClaimAsync(user, claim).ConfigureAwait(false);
            }
            else if (current.Value != claim.Value)
            {
                await _userManager.ReplaceClaimAsync(user, current, claim).ConfigureAwait(false);
            }
        }
    }

    /// <summary>True when the account carries a temporary password or an expired one.</summary>
    private static bool RequiresPasswordChange(ApplicationUser user)
    {
        if (user.MustChangePassword)
        {
            return true;
        }

        if (user.PasswordChangedOn is null)
        {
            return true;
        }

        return (DateTime.UtcNow - user.PasswordChangedOn.Value).TotalDays > AppConstants.PasswordExpiryDays;
    }

    private async Task RecordFailureAsync(string userName, string reason, CancellationToken cancellationToken)
    {
        await _audit.RecordLoginAsync(null, userName, false, reason, cancellationToken).ConfigureAwait(false);
        await _audit.LogAsync(AuditAction.LoginFailed, "User", null,
            $"Failed sign-in for '{userName}': {reason}.",
            isSuccessful: false, cancellationToken: cancellationToken).ConfigureAwait(false);

        _logger.LogWarning("Failed sign-in for {UserName}: {Reason}.", userName, reason);
    }

    /// <summary>
    /// Redirects only to a URL inside this application. An open redirect would
    /// let a crafted link bounce a signed-in user to an attacker's page.
    /// </summary>
    private IActionResult RedirectToLocal(string? returnUrl)
        => !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? Redirect(returnUrl)
            : RedirectToAction("Index", "Dashboard");
}
