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

        // Skip expiry checks for Account pages, Error, and Health endpoints
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
        if (context.Items.TryGetValue("TenantId", out var tenantIdObj) && tenantIdObj is int tenantId)
        {
            var orgResult = orgService.GetById(tenantId);
            if (!orgResult.Failed && orgResult.Value != null)
            {
                var org = orgResult.Value;

                // Check if org is active
                if (!org.IsActive)
                {
                    context.Response.Redirect("/Account/Suspended");
                    return;
                }

                // Check if trial has expired
                if (org.TrialEndsAtUtc.HasValue &&
                    org.TrialEndsAtUtc.Value < DateTime.UtcNow &&
                    (org.SubscriptionEndsAtUtc == null || org.SubscriptionEndsAtUtc.Value < DateTime.UtcNow))
                {
                    context.Response.Redirect("/Account/TrialExpired");
                    return;
                }
            }
        }

        await _next(context);
    }
}
