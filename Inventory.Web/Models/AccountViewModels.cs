using System.ComponentModel.DataAnnotations;

namespace Inventory.Web.Models;

/// <summary>Sign-in form.</summary>
public sealed class LoginViewModel
{
    [Required(ErrorMessage = "User name is required.")]
    [Display(Name = "User Name")]
    [StringLength(256)]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Remember me")]
    public bool RememberMe { get; set; }

    /// <summary>Validated with <c>Url.IsLocalUrl</c> before use.</summary>
    public string? ReturnUrl { get; set; }
}

/// <summary>Change-password form.</summary>
public sealed class ChangePasswordViewModel
{
    [Required(ErrorMessage = "Enter your current password.")]
    [DataType(DataType.Password)]
    [Display(Name = "Current Password")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter a new password.")]
    [StringLength(100, MinimumLength = 8,
        ErrorMessage = "The password must be at least 8 characters long.")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^\w\s]).{8,}$",
        ErrorMessage = "Use at least one upper case letter, one lower case letter, one digit and one symbol.")]
    [DataType(DataType.Password)]
    [Display(Name = "New Password")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirm the new password.")]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "The passwords do not match.")]
    [Display(Name = "Confirm New Password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    /// <summary>True when the change was forced rather than chosen.</summary>
    public bool IsRequired { get; set; }
}

/// <summary>Forgot-password form.</summary>
public sealed class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "Enter your registered e-mail address.")]
    [EmailAddress(ErrorMessage = "Enter a valid e-mail address.")]
    [Display(Name = "E-mail")]
    public string Email { get; set; } = string.Empty;
}
