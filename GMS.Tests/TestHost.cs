using GMS.Core.Abstractions;
using GMS.Core.DependencyInjection;
using GMS.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GMS.Tests;

/// <summary>
/// One in-memory GMS for a single test, wired through the real <c>AddGmsCore</c>.
/// </summary>
/// <remarks>
/// Built from production registration rather than by newing services up by hand, so a change to
/// a constructor cannot leave the tests passing against a shape the application no longer uses.
/// Only the three ambient dependencies are swapped - who is asking, which tenant they are in, and
/// what time it is - because those are exactly what a test needs to control.
///
/// These run against the in-memory store, so read what they prove narrowly. It applies no tenant
/// query filters and no foreign keys, so a test here cannot demonstrate that one tenant's rows are
/// hidden from another; that has to be exercised against PostgreSQL. What these do cover is the
/// business rules in GMS.Core: who is allowed to call what, subscription and trial arithmetic,
/// stock checks, and validation.
/// </remarks>
public sealed class TestHost : IDisposable
{
    private readonly ServiceProvider _root;
    private readonly IServiceScope _scope;

    public FakeClock Clock { get; } = new();
    public FakeCurrentUser User { get; } = new();
    public FakeTenantContext Tenant { get; } = new();

    public TestHost()
    {
        var services = new ServiceCollection();

        // Registered before AddGmsCore so the TryAdd* inside it leaves these alone.
        services.AddSingleton<IClock>(Clock);
        services.AddSingleton<ICurrentUser>(User);
        services.AddSingleton<ITenantContext>(Tenant);

        services.AddGmsCore();

        _root = services.BuildServiceProvider();
        _scope = _root.CreateScope();
    }

    public T Get<T>() where T : notnull => _scope.ServiceProvider.GetRequiredService<T>();

    /// <summary>Roles, permissions and the default organisation, as a fresh install would have.</summary>
    public TestHost WithBaseline()
    {
        Get<DataSeeder>().SeedBaseline();
        return this;
    }

    /// <summary>
    /// Creates an organisation as the operator would and leaves the caller scoped inside it,
    /// holding every permission. The usual starting point for testing tenant-level behaviour.
    /// </summary>
    public GMS.Core.Models.Organization NewTenant(string code = "acme", string name = "Acme Ltd")
    {
        User.IsPlatformOperator = true;
        var org = Get<OrganizationService>().CreateTrial(name, code, $"{code}@example.invalid");
        Assert.True(org.Succeeded, $"CreateTrial failed: {org.ErrorMessage}");

        User.IsPlatformOperator = false;
        User.AllPermissions = true;
        Tenant.OrganizationId = org.Value.Id;
        return org.Value;
    }

    public void Dispose()
    {
        _scope.Dispose();
        _root.Dispose();
    }
}

/// <summary>A clock the test moves on purpose. Trial and subscription rules are all date maths.</summary>
public sealed class FakeClock : IClock
{
    public DateTime UtcNow { get; set; } = new(2026, 1, 15, 9, 0, 0, DateTimeKind.Utc);

    public void Advance(TimeSpan by) => UtcNow = UtcNow.Add(by);
    public void AdvanceDays(int days) => Advance(TimeSpan.FromDays(days));
}

public sealed class FakeTenantContext : ITenantContext
{
    public int OrganizationId { get; set; } = 1;
    public bool IsSystemMode { get; set; }
}

/// <summary>
/// The caller. <see cref="IsPlatformOperator"/> and <see cref="AllPermissions"/> are separate on
/// purpose: the point of the platform flag is that no permission inside an organisation can ever
/// stand in for it, and a test that conflated them could not show that.
/// </summary>
public sealed class FakeCurrentUser : ICurrentUser
{
    public int? UserId { get; set; } = 1;
    public string UserName { get; set; } = "tester";
    public bool IsAuthenticated { get; set; } = true;
    public bool IsPlatformOperator { get; set; }
    public bool AllPermissions { get; set; }
    public HashSet<string> Permissions { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> Roles { get; } = new(StringComparer.OrdinalIgnoreCase);

    public bool HasPermission(string permissionCode) =>
        AllPermissions || Permissions.Contains(permissionCode);

    public bool IsInRole(string roleName) => Roles.Contains(roleName);
}
