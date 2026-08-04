using Inventory.BLL.Interfaces;
using Inventory.Common.Constants;
using Inventory.Common.Enums;
using Inventory.Common.Exceptions;
using Inventory.Common.Security;
using Inventory.Entities.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Inventory.Web.Infrastructure;

/// <summary>
/// Records every state-changing request on the audit trail.
/// <para>
/// The services already audit the business meaning of an operation. This filter
/// adds the missing half: the fact that an HTTP request was made, by whom, from
/// where and whether it succeeded — including the requests that fail model
/// validation and never reach a service.
/// </para>
/// </summary>
public sealed class AuditActionFilter : IAsyncActionFilter
{
    private static readonly string[] MutatingMethods = { "POST", "PUT", "PATCH", "DELETE" };

    private readonly IAuditService _audit;
    private readonly ILogger<AuditActionFilter> _logger;

    public AuditActionFilter(IAuditService audit, ILogger<AuditActionFilter> logger)
    {
        _audit = audit;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var request = context.HttpContext.Request;

        if (!MutatingMethods.Contains(request.Method, StringComparer.OrdinalIgnoreCase))
        {
            await next().ConfigureAwait(false);
            return;
        }

        var descriptor = $"{context.RouteData.Values["controller"]}/{context.RouteData.Values["action"]}";
        var executed = await next().ConfigureAwait(false);

        try
        {
            var failed = executed.Exception is not null;

            await _audit.LogAsync(
                AuditAction.Update,
                entityName: context.RouteData.Values["controller"]?.ToString(),
                entityId: context.RouteData.Values["id"]?.ToString(),
                description: $"{request.Method} {descriptor}",
                isSuccessful: !failed,
                errorMessage: executed.Exception?.Message,
                source: descriptor).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Request audit entry could not be written for {Action}.", descriptor);
        }
    }
}

/// <summary>
/// Forces a user whose password has expired, or who was created with a
/// temporary password, onto the change-password screen before they can use any
/// other part of the application.
/// </summary>
public sealed class PasswordExpiryFilter : IAsyncActionFilter
{
    private static readonly string[] AllowedActions =
    {
        "changepassword", "logout", "accessdenied", "login", "error", "statuscode"
    };

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var user = context.HttpContext.User;

        if (user.Identity?.IsAuthenticated != true)
        {
            await next().ConfigureAwait(false);
            return;
        }

        var mustChange = user.FindFirst(AppClaimTypes.MustChangePassword)?.Value;

        if (!string.Equals(mustChange, "true", StringComparison.OrdinalIgnoreCase))
        {
            await next().ConfigureAwait(false);
            return;
        }

        var action = context.RouteData.Values["action"]?.ToString()?.ToLowerInvariant() ?? string.Empty;

        if (AllowedActions.Contains(action))
        {
            await next().ConfigureAwait(false);
            return;
        }

        // AJAX callers get a status code they can act on; browsers get a redirect.
        if (IsAjax(context.HttpContext.Request))
        {
            context.Result = new JsonResult(AjaxResponse.Fail("Your password must be changed before continuing."))
            {
                StatusCode = StatusCodes.Status403Forbidden
            };

            return;
        }

        context.Result = new RedirectToActionResult("ChangePassword", "Account", new { required = true });
    }

    private static bool IsAjax(HttpRequest request)
        => string.Equals(request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Turns an unhandled exception into a response the caller can use: a JSON
/// envelope for AJAX, the error view for a browser. Business exceptions map to
/// their own status codes; everything else becomes a 500 with a generic message,
/// so internal details never reach the user.
/// </summary>
public sealed class GlobalExceptionFilter : IAsyncExceptionFilter
{
    private readonly ILogger<GlobalExceptionFilter> _logger;
    private readonly IWebHostEnvironment _environment;

    public GlobalExceptionFilter(ILogger<GlobalExceptionFilter> logger, IWebHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public Task OnExceptionAsync(ExceptionContext context)
    {
        var exception = context.Exception;

        var (statusCode, message) = exception switch
        {
            ValidationException validation => (StatusCodes.Status400BadRequest, validation.Message),
            EntityNotFoundException notFound => (StatusCodes.Status404NotFound, notFound.Message),
            ForbiddenOperationException forbidden => (StatusCodes.Status403Forbidden, forbidden.Message),
            BusinessRuleException rule => (StatusCodes.Status409Conflict, rule.Message),
            _ => (StatusCodes.Status500InternalServerError,
                  "An unexpected error occurred. The support team has been notified.")
        };

        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception on {Path}.", context.HttpContext.Request.Path);
        }
        else
        {
            _logger.LogWarning("{ExceptionType} on {Path}: {Message}",
                exception.GetType().Name, context.HttpContext.Request.Path, exception.Message);
        }

        if (IsAjax(context.HttpContext.Request))
        {
            context.Result = new JsonResult(AjaxResponse.Fail(
                _environment.IsDevelopment() ? exception.Message : message))
            {
                StatusCode = statusCode
            };
        }
        else
        {
            context.Result = new RedirectToActionResult("Error", "Home", new { code = statusCode });
        }

        context.ExceptionHandled = true;
        return Task.CompletedTask;
    }

    private static bool IsAjax(HttpRequest request)
        => string.Equals(request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
}
