using System.Security.Claims;
using EMG.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace EMG.Api.Modules.Security;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = true)]
public sealed class AllowWhenPasswordChangeRequiredAttribute : Attribute;

public sealed class MustChangePasswordMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, EmgDbContext db)
    {
        var isAuthenticated = context.User.Identity?.IsAuthenticated == true;
        var mustChangePassword = false;
        if (isAuthenticated)
        {
            if (!int.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            {
                await WriteBlockedResponseAsync(context, "AUTHENTICATION_STATE_INVALID", StatusCodes.Status403Forbidden);
                return;
            }

            bool? currentDatabaseValue;
            try
            {
                currentDatabaseValue = await db.Users.AsNoTracking()
                    .Where(user => user.Id == userId)
                    .Select(user => (bool?)user.MustChangePassword)
                    .SingleOrDefaultAsync(context.RequestAborted);
            }
            catch (Exception) when (!context.RequestAborted.IsCancellationRequested)
            {
                await WriteBlockedResponseAsync(context, "AUTHORIZATION_STATE_UNAVAILABLE", StatusCodes.Status503ServiceUnavailable);
                return;
            }

            if (currentDatabaseValue is null)
            {
                await WriteBlockedResponseAsync(context, "AUTHENTICATION_STATE_INVALID", StatusCodes.Status403Forbidden);
                return;
            }

            // The signed claim is an additional restriction, never an override
            // for the current database state.
            mustChangePassword = currentDatabaseValue.Value ||
                string.Equals(
                    context.User.FindFirstValue("must_change_password"),
                    "true",
                    StringComparison.OrdinalIgnoreCase);
        }

        var isAllowedBeforeChange = context.GetEndpoint()?.Metadata
            .GetMetadata<AllowWhenPasswordChangeRequiredAttribute>() is not null;

        if (mustChangePassword && !isAllowedBeforeChange)
        {
            await WriteBlockedResponseAsync(context, "PASSWORD_CHANGE_REQUIRED", StatusCodes.Status403Forbidden);
            return;
        }

        await next(context);
    }

    private static Task WriteBlockedResponseAsync(HttpContext context, string code, int statusCode)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";
        return context.Response.WriteAsJsonAsync(new
        {
            message = code == "PASSWORD_CHANGE_REQUIRED"
                ? "يجب تغيير كلمة المرور قبل استخدام بقية النظام."
                : "تعذر التحقق من حالة الحساب الحالية.",
            code
        }, context.RequestAborted);
    }
}
