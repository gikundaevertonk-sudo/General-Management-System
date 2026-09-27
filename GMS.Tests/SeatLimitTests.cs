using GMS.Core.Common;
using GMS.Core.Contracts;
using GMS.Core.Services;

namespace GMS.Tests;

/// <summary>
/// The operator setting how many users an organisation has paid for.
/// </summary>
/// <remarks>
/// Separate from <see cref="OrganizationService.UpdateDetails"/> on purpose - what a customer is
/// called and what they are paying for are different questions, and one form posting both is how a
/// stale field silently overwrites the other. See also <see cref="UserCapTests"/>, which covers the
/// enforcement; these cover the operator's side of it.
/// </remarks>
public class SeatLimitTests
{
    private static Result<GMS.Core.Models.User> AddUser(TestHost host, string name) =>
        host.Get<UserService>().Create(new UserInput
        {
            UserName = name,
            FullName = name,
            Email = $"{name}@example.invalid",
            RoleId = host.Get<RoleService>().List().Value.First().Role.Id,
            IsActive = true,
        }, "Seats#2026");

    [Fact]
    public void The_operator_sets_the_limit_and_is_told_what_is_in_use()
    {
        using var host = new TestHost().WithBaseline();
        var org = host.NewTenant("seats");
        host.User.IsPlatformOperator = true;

        var result = host.Get<OrganizationService>().SetUserLimit(org.Id, 7);

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.Value);        // onboarding's administrator
        Assert.Equal(7, host.Get<OrganizationService>().GetById(org.Id).Value.MaxUsers);
    }

    [Fact]
    public void Paying_for_seven_gets_seven()
    {
        using var host = new TestHost().WithBaseline();
        var org = host.NewTenant("seven");
        host.User.IsPlatformOperator = true;
        Assert.True(host.Get<OrganizationService>().SetUserLimit(org.Id, 7).Succeeded);
        host.User.IsPlatformOperator = false;
        host.User.AllPermissions = true;

        for (var i = 2; i <= 7; i++)
            Assert.True(AddUser(host, $"user{i}").Succeeded, $"user{i} refused");

        Assert.True(AddUser(host, "user8").Failed);
    }

    [Fact]
    public void A_limit_below_one_is_refused()
    {
        using var host = new TestHost().WithBaseline();
        var org = host.NewTenant("zero");
        host.User.IsPlatformOperator = true;

        Assert.True(host.Get<OrganizationService>().SetUserLimit(org.Id, 0).Failed);
        Assert.True(host.Get<OrganizationService>().SetUserLimit(org.Id, -3).Failed);
    }

    [Fact]
    public void Downgrading_below_the_head_count_is_recorded_and_locks_nobody_out()
    {
        using var host = new TestHost().WithBaseline();
        var org = host.NewTenant("downgrade");
        host.User.IsPlatformOperator = true;
        host.Get<OrganizationService>().SetUserLimit(org.Id, 7);
        host.User.IsPlatformOperator = false;
        host.User.AllPermissions = true;
        for (var i = 2; i <= 7; i++) Assert.True(AddUser(host, $"u{i}").Succeeded);

        // They drop to five seats while still holding seven users.
        host.User.IsPlatformOperator = true;
        var result = host.Get<OrganizationService>().SetUserLimit(org.Id, 5);

        Assert.True(result.Succeeded);
        Assert.Equal(7, result.Value);          // the caller is told, so it can say so
        // Nobody removed, nobody deactivated - the only consequence is no room to grow.
        Assert.Equal(7, host.Get<OrganizationService>().GetStats(org.Id).Value.UserCount);
        host.User.IsPlatformOperator = false;
        Assert.True(AddUser(host, "u8").Failed);
    }

    [Fact]
    public void Editing_the_name_cannot_change_the_limit()
    {
        using var host = new TestHost().WithBaseline();
        var org = host.NewTenant("rename");
        host.User.IsPlatformOperator = true;
        host.Get<OrganizationService>().SetUserLimit(org.Id, 9);

        host.Get<OrganizationService>().UpdateDetails(org.Id, "Renamed Ltd", "new@example.invalid", 9);

        var after = host.Get<OrganizationService>().GetById(org.Id).Value;
        Assert.Equal("Renamed Ltd", after.Name);
        Assert.Equal(9, after.MaxUsers);
    }

    [Fact]
    public void A_tenant_admin_cannot_raise_their_own_limit()
    {
        using var host = new TestHost().WithBaseline();
        var org = host.NewTenant("cheeky");   // caller left as tenant admin with all permissions

        Assert.True(host.Get<OrganizationService>().SetUserLimit(org.Id, 500).Failed);

        host.User.IsPlatformOperator = true;
        Assert.Equal(5, host.Get<OrganizationService>().GetById(org.Id).Value.MaxUsers);
    }
}
