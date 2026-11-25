using System.Security.Cryptography;

namespace EMG.Api.Modules.Security;

public sealed class CsrfProtectionMiddleware(RequestDelegate next)
{
    private static readonly PathString[] AnonymousPrefixes = ["/api/account/login", "/api/account/csrf", "/api/account/password-reset"];

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true &&
            (HttpMethods.IsPost(context.Request.Method) || HttpMethods.IsPut(context.Request.Method) ||
             HttpMethods.IsPatch(context.Request.Method) || HttpMethods.IsDelete(context.Request.Method)))
        {
            var path = context.Request.Path;
            if (!AnonymousPrefixes.Any(prefix => path.StartsWithSegments(prefix)))
            {
                context.Request.Cookies.TryGetValue("emg_csrf", out var cookie);
                context.Request.Headers.TryGetValue("X-CSRF-TOKEN", out var header);
                var normalizedCookie = string.IsNullOrWhiteSpace(cookie) ? null : Uri.UnescapeDataString(cookie);
                var normalizedHeader = header.Count == 1 ? Uri.UnescapeDataString(header[0]!) : null;
                if (string.IsNullOrWhiteSpace(normalizedCookie) || normalizedHeader is null ||
                    !CryptographicOperations.FixedTimeEquals(System.Text.Encoding.UTF8.GetBytes(normalizedCookie), System.Text.Encoding.UTF8.GetBytes(normalizedHeader)))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsJsonAsync(new { code = "CSRF_REQUIRED", message = "رمز الحماية غير صالح أو مفقود." });
                    return;
                }
            }
        }
        await next(context);
    }
}
