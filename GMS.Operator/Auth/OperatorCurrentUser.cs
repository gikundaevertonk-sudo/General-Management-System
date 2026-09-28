using GMS.Core.Abstractions;

namespace GMS.Operator.Auth;

/// <summary>
/// <see cref="ICurrentUser"/> for the operator console. The signed-in principal here is the
/// system owner, never a tenant user.
/// </summary>
/// <remarks>
/// This application has exactly one cookie scheme and one kind of visitor, so
/// <see cref="IsPlatformOperator"/> is simply "is somebody signed in". That is the whole point
/// of splitting the console out of GMS.Web: there, the operator identity shared a
/// ClaimsPrincipal with tenant identities and had to be told apart by authentication scheme,
/// which is fragile — and was the cause of a sign-out that silently returned 400. Here there is
/// nothing to confuse it with.
///
/// The permission methods return false rather than true. The operator is not a member of any
/// organization and holds no permission inside one; every action the console performs is
/// guarded by ServiceBase.DeniedPlatform, which reads IsPlatformOperator alone. Answering true
/// would hand the console tenant-level authority it has no business having.
/// </remarks>
public sealed class OperatorCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private bool SignedIn => accessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

    /// <summary>
    /// Always null: the operator has no row in <c>users</c>. Audit columns that record it
    /// (<c>UpdatedByUserId</c>) are nullable for exactly this case.
    /// </summary>
    public int? UserId => null;

    public string UserName => accessor.HttpContext?.User?.Identity?.Name ?? "operator";

    public bool IsAuthenticated => SignedIn;

    public bool HasPermission(string permissionCode) => false;

    public bool IsInRole(string roleName) => false;

    /// <summary>
    /// Always null. Shops belong to a tenant, and the operator is not a member of one; the console
    /// has no screen that works at a shop and nothing here should ever be scoped to one.
    /// </summary>
    public int? ShopId => null;

    public bool IsPlatformOperator => SignedIn;
}
