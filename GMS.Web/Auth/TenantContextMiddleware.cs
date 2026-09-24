using System.Security.Claims;
using GMS.Core.Data;

namespace GMS.Web.Auth;

/// <summary>
/// Middleware that extracts the TenantId (organization_id) from the authentication cookie
/// and makes it available in HttpContext.Items for the duration of the request.
/// This ensures WebTenantContext can access the tenant ID even on subsequent requests.
/// </summary>
/// <remarks>
/// This used to also push the value into a static on GmsDbContext. It must not: a static is
/// shared by every in-flight request, so two users from different organizations hitting the
/// app at the same time would overwrite each other's tenant and could be served the wrong
/// organization's data. GmsDbContext now reads the scoped ITenantContext (WebTenantContext),
/// which resolves per request from HttpContext, so this middleware only has to populate
/// HttpContext.Items.
/// </remarks>
public class TenantContextMiddleware
{
    private readonly RequestDelegate _next;

    public TenantContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task Invoke(HttpContext context)
    {
        // Already set during sign-in? Leave it. Otherwise lift it off the cookie claim so
        // WebTenantContext can read it without re-parsing the claim on every resolve.
        var alreadySet = context.Items.ContainsKey("TenantId") && context.Items["TenantId"] is int;

        if (!alreadySet && context.User?.Identity?.IsAuthenticated == true)
        {
            var claim = context.User.FindFirstValue(Claims.OrganizationId);
            if (claim != null && int.TryParse(claim, out var claimId))
            {
                context.Items["TenantId"] = claimId;
            }
        }

        return _next(context);
    }
}
