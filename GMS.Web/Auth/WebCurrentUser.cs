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
}

/// <summary>Custom claim types used by the cookie identity.</summary>
public static class Claims
{
    public const string Permission = "perm";
    public const string FullName = "fullname";
    public const string MustChangePassword = "mustchange";
    public const string OrganizationId = "org_id";

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
        claims.AddRange(user.Permissions.Select(code => new Claim(Permission, code)));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, authScheme));
    }
}
