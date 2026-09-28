using System.Security.Claims;
using GMS.Core.Abstractions;
using GMS.Core.Contracts;

namespace GMS.Web.Auth;

/// <summary>
/// Web implementation of <see cref="ICurrentUser"/>. Reads the signed-in user from
/// the authentication cookie's claims. When there is no HTTP context (application
/// start-up / seeding) it acts as a trusted system principal.
/// </summary>
public sealed class WebCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? User => accessor.HttpContext?.User;

    private bool IsSystemContext => accessor.HttpContext is null;

    public int? UserId
    {
        get
        {
            var raw = User?.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(raw, out var id) ? id : null;
        }
    }

    public string UserName =>
        IsSystemContext ? "system" : User?.Identity?.Name ?? string.Empty;

    public bool IsAuthenticated =>
        IsSystemContext || (User?.Identity?.IsAuthenticated ?? false);

    public bool HasPermission(string permissionCode) =>
        IsSystemContext || (User?.HasClaim(Claims.Permission, permissionCode) ?? false);

    public bool IsInRole(string roleName) =>
        IsSystemContext || (User?.IsInRole(roleName) ?? false);

    /// <summary>
    /// The shop claim written at sign-in, or null for a user with organization-wide access.
    /// </summary>
    /// <remarks>
    /// Read from the cookie rather than the database on every call, like every other part of this
    /// identity. The cost is that moving someone between shops only takes effect at their next
    /// sign-in — acceptable, because the same is already true of changing their role. During
    /// start-up seeding there is no HTTP context and so no claim, which correctly leaves the
    /// seeder unconfined.
    /// </remarks>
    public int? ShopId =>
        int.TryParse(User?.FindFirstValue(Claims.ShopId), out var id) ? id : null;

    /// <summary>
    /// Always false. This application has no operator surface at all.
    /// </summary>
    /// <remarks>
    /// The system owner's console is GMS.Operator, a separate application. Nothing that reaches
    /// across organizations - listing every tenant, resetting another tenant's password,
    /// deleting an organization - is gated by a permission code; every one of them calls
    /// ServiceBase.DeniedPlatform, which reads this. Returning a hard false therefore means no
    /// bug anywhere in the tenant application can reach those operations, not even for a user
    /// who has been granted every permission inside their own organization.
    ///
    /// Do not reintroduce a condition here. A claim, a role name or a configuration flag that
    /// could flip it to true would put the operator's authority back inside the product tenants
    /// use, which is the arrangement this was split apart to end.
    /// </remarks>
    public bool IsPlatformOperator => false;
}

/// <summary>Custom claim types used by the cookie identity.</summary>
public static class Claims
{
    public const string Permission = "perm";
    public const string FullName = "fullname";
    public const string MustChangePassword = "mustchange";
    public const string OrganizationId = "org_id";
    public const string ShopId = "shop_id";
    public const string ShopName = "shop_name";

    /// <summary>Builds the cookie identity for a freshly authenticated user.</summary>
    public static ClaimsPrincipal BuildPrincipal(AuthenticatedUser user, string authScheme)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, user.UserName),
            new(ClaimTypes.Role, user.RoleName),
            new(FullName, string.IsNullOrWhiteSpace(user.FullName) ? user.UserName : user.FullName),
            new(MustChangePassword, user.MustChangePassword ? "1" : "0"),
            new(OrganizationId, user.OrganizationId.ToString()),
        };
        // Omitted entirely for an unpinned user, so WebCurrentUser.ShopId reads null rather than
        // having to treat some placeholder value as "no shop".
        if (user.ShopId is int shopId)
        {
            claims.Add(new Claim(ShopId, shopId.ToString()));
            claims.Add(new Claim(ShopName, user.ShopName));
        }
        claims.AddRange(user.Permissions.Select(code => new Claim(Permission, code)));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, authScheme));
    }
}
