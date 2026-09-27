using GMS.Core.Services;
using Microsoft.AspNetCore.Authorization;

namespace GMS.Web.Auth;

/// <summary>
/// Middleware that checks if the user's organization trial/subscription has expired.
/// Blocks access to protected pages if the trial is expired.
/// Allows access to Account pages (logout, etc.) and Error pages regardless of expiry.
/// </summary>
public class TrialExpiryMiddleware
{
    private readonly RequestDelegate _next;

    public TrialExpiryMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context, OrganizationService orgService)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // Skip expiry checks for Account pages, Error, and Health endpoints. There is no
        // /Platform exemption any more: the owner's console is GMS.Operator, a separate
        // application, so no request reaching this middleware can belong to it.
        if (path.StartsWith("/Account/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/Error", StringComparison.OrdinalIgnoreCase) ||
            path == "/health")
        {
            await _next(context);
            return;
        }

        // Skip if user is not authenticated
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        // Extract TenantId from context
        if (context.Items.TryGetValue("TenantId", out var tenantIdObj) && tenantIdObj is int)
        {
            // Reads the caller's own organization with no permission check. This used to call
            // GetById, which is gated on an administrative permission, so for any user without
            // it the call came back Forbidden and the whole check below was skipped - suspending
            // an organization or letting its trial lapse only ever blocked its administrators
            // while ordinary staff carried on working.
            var orgResult = orgService.GetOwnOrganization();
            if (!orgResult.Failed && orgResult.Value != null)
            {
                var org = orgResult.Value;

                // Check if org is active
                if (!org.IsActive)
                {
                    context.Response.Redirect("/Account/Suspended");
                    return;
                }

                // One shared definition with the operator console, so what the owner sees
                // marked unpaid is exactly who gets cut off.
                if (OrganizationService.IsLapsed(org, DateTime.UtcNow))
                {
                    context.Response.Redirect("/Account/TrialExpired");
                    return;
                }
            }
        }

        await _next(context);
    }
}
