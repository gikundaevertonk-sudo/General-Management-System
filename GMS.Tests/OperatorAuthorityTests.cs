using GMS.Core.Services;

namespace GMS.Tests;

/// <summary>
/// Who is allowed to reach across organisations.
/// </summary>
/// <remarks>
/// Every one of these calls is guarded by ServiceBase.DeniedPlatform, which reads
/// ICurrentUser.IsPlatformOperator and nothing else. The tests below hold a caller with *every*
/// permission inside their own organisation and show they are still refused - that is the whole
/// design, and if a permission code were ever accepted in place of the platform flag, one
/// tenant's administrator could suspend or delete every other tenant.
/// </remarks>
public class OperatorAuthorityTests
{
    [Fact]
    public void A_tenant_admin_with_every_permission_cannot_list_other_organisations()
    {
        using var host = new TestHost().WithBaseline();
        host.User.AllPermissions = true;
        host.User.IsPlatformOperator = false;

        var result = host.Get<OrganizationService>().ListAll();

        Assert.True(result.Failed);
    }

    [Theory]
    [InlineData("suspend")]
    [InlineData("activate")]
    [InlineData("delete")]
    [InlineData("update details")]
    [InlineData("reset another tenant's password")]
    [InlineData("read another tenant's figures")]
    public void A_tenant_admin_is_refused_every_cross_tenant_action(string action)
    {
        using var host = new TestHost().WithBaseline();
        var org = host.NewTenant("victim");

        // NewTenant leaves the caller as an ordinary tenant admin holding all permissions.
        Assert.True(host.User.AllPermissions);
        Assert.False(host.User.IsPlatformOperator);

        var orgs = host.Get<OrganizationService>();
        var failed = action switch
        {
            "suspend" => orgs.Suspend(org.Id).Failed,
            "activate" => orgs.Activate(org.Id).Failed,
            "delete" => orgs.DeleteOrganization(org.Id).Failed,
            "update details" => orgs.UpdateDetails(org.Id, "Renamed", "x@y.z", 10).Failed,
            "reset another tenant's password" => orgs.ResetUserPassword(org.Id, 1, "Abcdef12").Failed,
            "read another tenant's figures" => orgs.GetStats(org.Id).Failed,
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };

        Assert.True(failed, $"a tenant admin was allowed to {action}");
    }

    [Fact]
    public void The_operator_can_do_what_the_tenant_admin_cannot()
    {
        using var host = new TestHost().WithBaseline();
        var org = host.NewTenant("acme");

        host.User.IsPlatformOperator = true;
        host.User.AllPermissions = false;   // the operator holds no tenant permissions at all

        Assert.True(host.Get<OrganizationService>().ListAll().Succeeded);
        Assert.True(host.Get<OrganizationService>().GetStats(org.Id).Succeeded);
        Assert.True(host.Get<OrganizationService>().Suspend(org.Id).Succeeded);
    }

    [Fact]
    public void The_default_organisation_cannot_be_deleted()
    {
        using var host = new TestHost().WithBaseline();
        host.User.IsPlatformOperator = true;

        var all = host.Get<OrganizationService>().ListAll();
        var seeded = all.Value.Single(o =>
            string.Equals(o.Code, DataSeeder.DefaultOrganizationCode, StringComparison.OrdinalIgnoreCase));

        var result = host.Get<OrganizationService>().DeleteOrganization(seeded.Id);

        Assert.True(result.Failed);
        Assert.Contains("default", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Deleting_an_organisation_removes_its_users()
    {
        using var host = new TestHost().WithBaseline();
        var org = host.NewTenant("doomed");

        host.User.IsPlatformOperator = true;
        Assert.Equal(1, host.Get<OrganizationService>().GetStats(org.Id).Value.UserCount);

        Assert.True(host.Get<OrganizationService>().DeleteOrganization(org.Id).Succeeded);

        // Not a cascade: the in-memory store has no foreign keys, so if DeleteOrganization did
        // not remove these rows itself nothing would, and the tenant's users would linger.
        Assert.Equal(0, host.Get<OrganizationService>().GetStats(org.Id).Value.UserCount);
        Assert.True(host.Get<OrganizationService>().GetById(org.Id).Failed);
    }
}
