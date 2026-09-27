using GMS.Core.Common;
using GMS.Core.Contracts;
using GMS.Core.Enums;
using GMS.Core.Services;

namespace GMS.Tests;

/// <summary>
/// The batched figures behind the operator's organisation list.
/// </summary>
/// <remarks>
/// It replaced a GetStats call per row, so the thing worth proving is that it returns the same
/// answers - and that an organisation with nothing in it is absent rather than present-and-zero,
/// because the list page has to tell that apart from "the counts could not be read".
/// </remarks>
public class ListStatsTests
{
    [Fact]
    public void It_agrees_with_GetStats_for_every_organisation()
    {
        using var host = new TestHost().WithBaseline();
        var first = host.NewTenant("alpha");
        host.Get<ProductService>().Create(new ProductInput
        {
            Sku = "A1", Name = "Thing", UnitOfMeasure = "ea", UnitPrice = 1m, CostPrice = 1m, IsActive = true,
        });
        host.Get<CustomerService>().Create(new CustomerInput { Code = "C1", Name = "Buyer", IsActive = true });
        var second = host.NewTenant("beta");

        host.User.IsPlatformOperator = true;
        var batched = host.Get<OrganizationService>().ListStats();
        Assert.True(batched.Succeeded);

        foreach (var org in host.Get<OrganizationService>().ListAll().Value)
        {
            var one = host.Get<OrganizationService>().GetStats(org.Id).Value;
            var many = batched.Value.GetValueOrDefault(org.Id) ?? new TenantStats();

            Assert.Equal(one.UserCount, many.UserCount);
            Assert.Equal(one.ProductCount, many.ProductCount);
            Assert.Equal(one.TransactionCount, many.TransactionCount);
            Assert.Equal(one.CustomerCount, many.CustomerCount);
            Assert.Equal(one.LastSignInUtc, many.LastSignInUtc);
        }
    }

    [Fact]
    public void Counts_are_kept_to_the_right_organisation()
    {
        using var host = new TestHost().WithBaseline();
        var alpha = host.NewTenant("alpha");
        host.Get<ProductService>().Create(new ProductInput
        {
            Sku = "A1", Name = "Alpha only", UnitOfMeasure = "ea", UnitPrice = 1m, CostPrice = 1m, IsActive = true,
        });
        var beta = host.NewTenant("beta");

        host.User.IsPlatformOperator = true;
        var stats = host.Get<OrganizationService>().ListStats().Value;

        Assert.Equal(1, stats[alpha.Id].ProductCount);
        Assert.Equal(0, stats.GetValueOrDefault(beta.Id)?.ProductCount ?? 0);
    }

    [Fact]
    public void It_is_refused_to_anyone_but_the_operator()
    {
        using var host = new TestHost().WithBaseline();
        host.NewTenant("acme");   // leaves the caller a tenant admin with every permission

        Assert.True(host.Get<OrganizationService>().ListStats().Failed);
    }

    [Fact]
    public void An_organisation_with_users_always_has_an_entry()
    {
        // The list page reads a missing entry as "nothing at all", which for users means a tenant
        // nobody can sign in to. Onboarding always creates an administrator, so a healthy tenant
        // must never be absent or it would be reported as broken.
        using var host = new TestHost().WithBaseline();
        var org = host.NewTenant("healthy");

        host.User.IsPlatformOperator = true;
        var stats = host.Get<OrganizationService>().ListStats().Value;

        Assert.True(stats.ContainsKey(org.Id));
        Assert.Equal(1, stats[org.Id].UserCount);
    }

    [Fact]
    public void Last_sign_in_is_the_most_recent_across_the_tenants_users()
    {
        using var host = new TestHost().WithBaseline();
        var org = host.NewTenant("signins");
        host.User.IsPlatformOperator = true;
        var admin = host.Get<OrganizationService>().ListUsers(org.Id).Value.Single();
        Assert.True(host.Get<OrganizationService>().ResetUserPassword(org.Id, admin.Id, "Signin#2026").Succeeded);
        host.User.IsPlatformOperator = false;

        host.Clock.AdvanceDays(3);
        Assert.True(host.Get<AuthService>().SignInWithTenant(admin.UserName, "Signin#2026", "signins").Succeeded);
        var expected = host.Clock.UtcNow;

        host.User.IsPlatformOperator = true;
        var stats = host.Get<OrganizationService>().ListStats().Value;

        Assert.Equal(expected, stats[org.Id].LastSignInUtc);
    }
}
