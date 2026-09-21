using System.Security.Claims;
using GMS.Core.Data;

namespace GMS.Web.Auth;

/// <summary>
/// Middleware that extracts the TenantId (organization_id) from the authentication cookie
/// and makes it available in HttpContext.Items for the duration of the request.
/// Also sets the static tenant context in GmsDbContext so global query filters work correctly.
/// This ensures WebTenantContext can access the tenant ID even on subsequent requests.
/// </summary>
public class TenantContextMiddleware
{
    private readonly RequestDelegate _next;

    public TenantContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task Invoke(HttpContext context)
    {
        // If TenantId is already set (e.g., during login), use it; otherwise extract from cookie
        int? tenantId = null;

        if (context.Items.ContainsKey("TenantId") && context.Items["TenantId"] is int existingId)
        {
            tenantId = existingId;
        }
        else if (context.User?.Identity?.IsAuthenticated == true)
        {
            var claim = context.User.FindFirstValue(Claims.OrganizationId);
            if (claim != null && int.TryParse(claim, out var claimId))
            {
                tenantId = claimId;
                context.Items["TenantId"] = tenantId;
            }
        }

        // Set the static tenant ID in GmsDbContext so global query filters use it
        if (tenantId.HasValue)
        {
            GmsDbContext.SetTenantId(tenantId.Value);
        }

        return _next(context);
    }
}
