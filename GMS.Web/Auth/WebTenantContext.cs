using System.Security.Claims;
using GMS.Core.Abstractions;
using GMS.Core.Models;
using Microsoft.AspNetCore.Http;

namespace GMS.Web.Auth;

public class WebTenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private int? _cachedOrgId;

    public WebTenantContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public int OrganizationId
    {
        get
        {
            if (_cachedOrgId.HasValue) return _cachedOrgId.Value;

            var context = _httpContextAccessor?.HttpContext;

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
