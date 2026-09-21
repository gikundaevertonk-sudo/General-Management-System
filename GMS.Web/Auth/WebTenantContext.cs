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
            if (context?.Items.TryGetValue("TenantId", out var tenantId) == true && tenantId is int orgId)
            {
                _cachedOrgId = orgId;
                return orgId;
            }

            throw new InvalidOperationException("TenantId not found in HttpContext");
        }
    }

    public bool IsSystemMode => false;
}
