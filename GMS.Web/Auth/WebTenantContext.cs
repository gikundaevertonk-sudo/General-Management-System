using System.Security.Claims;
using GMS.Core.Abstractions;
using GMS.Core.Models;
using Microsoft.AspNetCore.Http;

namespace GMS.Web.Auth;

public class WebTenantContext : ITenantContext
{
    /// <summary>The organization DataSeeder creates for a fresh installation.</summary>
    private const int DefaultOrganizationId = 1;

    private readonly IHttpContextAccessor _httpContextAccessor;
    private int? _cachedOrgId;

    public WebTenantContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// The organization the current request belongs to.
    /// </summary>
    /// <remarks>
    /// There is deliberately no fallback while a request is in flight: an unauthenticated or
    /// claim-less request must fail rather than be silently scoped to somebody else's
    /// organization. The single exception is when there is no HttpContext at all, which means
    /// we are not serving anyone — start-up seeding in Program.SeedData runs in its own scope,
    /// the same situation WebCurrentUser already treats as a system principal. GmsDbContext
    /// reads this for every tenant-scoped query, so throwing there would stop the application
    /// from starting.
    /// </remarks>
    public int OrganizationId
    {
        get
        {
            if (_cachedOrgId.HasValue) return _cachedOrgId.Value;

            var context = _httpContextAccessor?.HttpContext;

            // Not inside a request: start-up seeding. Scope it to the default organization.
            if (context is null) return DefaultOrganizationId;

            // First check HttpContext.Items (set during sign-in)
            if (context?.Items.TryGetValue("TenantId", out var tenantId) == true && tenantId is int orgId)
            {
                _cachedOrgId = orgId;
                return orgId;
            }

            // Then check the authentication cookie claim
            var claim = context?.User?.FindFirstValue(Claims.OrganizationId);
            if (claim != null && int.TryParse(claim, out var claimOrgId))
            {
                _cachedOrgId = claimOrgId;
                return claimOrgId;
            }

            throw new InvalidOperationException("TenantId not found in HttpContext or claims");
        }
    }

    public bool IsSystemMode => false;
}
