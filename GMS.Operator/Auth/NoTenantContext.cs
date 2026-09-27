using GMS.Core.Abstractions;

namespace GMS.Operator.Auth;

/// <summary>
/// <see cref="ITenantContext"/> for the operator console, which belongs to no organization.
/// </summary>
/// <remarks>
/// Asking for the current tenant is a programming error here, so it throws with a message that
/// says what to do instead. Every query the console runs must go through
/// <c>IRepository.QueryAcrossTenants</c> and match <c>OrganizationId</c> itself; the console is
/// looking at organizations it is not a member of, so a tenant-filtered <c>Query()</c> would
/// scope its results away to nothing even if a tenant id could be invented for it.
///
/// GMS.Web had the same behaviour by accident — WebTenantContext threw when a request carried no
/// organization claim, which is every operator request — and the console only survived because
/// it happened never to run a filtered query. Making it explicit means the failure, if anyone
/// introduces one, is a clear exception rather than a puzzle.
/// </remarks>
public sealed class NoTenantContext : ITenantContext
{
    public int OrganizationId =>
        throw new InvalidOperationException(
            "The operator console has no tenant. Use IRepository.QueryAcrossTenants() and " +
            "filter on OrganizationId explicitly instead of Query().");

    public bool IsSystemMode => false;
}
