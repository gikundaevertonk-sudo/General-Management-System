using GMS.Core.Common;
using GMS.Core.Contracts;
using GMS.Core.Services;

namespace GMS.Tests;

/// <summary>
/// The per-organisation user limit.
/// </summary>
/// <remarks>
/// This is what the operator console sells and displays, so it has to bite. It did not: MaxUsers
/// was stored, shown on the console as "2/10 users" and editable there, but nothing read it. An
/// integration run against PostgreSQL created seven users in an organisation capped at five and the
/// console cheerfully rendered "7/5 users". These tests exist so that cannot come back.
/// </remarks>
public class UserCapTests
{
    private static Result<GMS.Core.Models.User> AddUser(TestHost host, string name) =>
        host.Get<UserService>().Create(new UserInput
        {
            UserName = name,
            FullName = name,
            Email = $"{name}@example.invalid",
            RoleId = host.Get<RoleService>().List().Value.First().Role.Id,
            IsActive = true,
        }, "CapTest#2026");

    [Fact]
    public void Users_can_be_added_up_to_the_limit()
    {
        using var host = new TestHost().WithBaseline();
        var org = host.NewTenant("capped");          // CreateTrial starts organisations at 5

        Assert.Equal(5, org.MaxUsers);
        // Onboarding already created the tenant's first administrator, so four places are left.
        for (var i = 2; i <= 5; i++)
        {
            var result = AddUser(host, $"user{i}");
            Assert.True(result.Succeeded, $"user{i} was refused: {result.ErrorMessage}");
        }
    }

    [Fact]
    public void The_user_after_the_limit_is_refused()
    {
        using var host = new TestHost().WithBaseline();
        host.NewTenant("full");

        for (var i = 2; i <= 5; i++) Assert.True(AddUser(host, $"user{i}").Succeeded);

        var overflow = AddUser(host, "onetoomany");

        Assert.True(overflow.Failed);
        Assert.Contains("limited to 5", overflow.ErrorMessage);
    }

    [Fact]
    public void Raising_the_limit_lets_more_in()
    {
        using var host = new TestHost().WithBaseline();
        var org = host.NewTenant("growing");
        for (var i = 2; i <= 5; i++) Assert.True(AddUser(host, $"user{i}").Succeeded);
        Assert.True(AddUser(host, "blocked").Failed);

        // What the operator does on the organisation's page.
        host.User.IsPlatformOperator = true;
        Assert.True(host.Get<OrganizationService>().UpdateDetails(org.Id, org.Name, org.Email, 7).Succeeded);
        host.User.IsPlatformOperator = false;

        Assert.True(AddUser(host, "user6").Succeeded);
        Assert.True(AddUser(host, "user7").Succeeded);
        Assert.True(AddUser(host, "user8").Failed);
    }

    [Fact]
    public void One_organisation_reaching_its_limit_does_not_block_another()
    {
        using var host = new TestHost().WithBaseline();

        var first = host.NewTenant("first");
        for (var i = 2; i <= 5; i++) Assert.True(AddUser(host, $"a{i}").Succeeded);
        Assert.True(AddUser(host, "a6").Failed);

        // A second tenant, counted on its own.
        var second = host.NewTenant("second");
        Assert.True(AddUser(host, "b2").Succeeded);
    }
}
