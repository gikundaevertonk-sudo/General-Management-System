using GMS.Core.Contracts;
using GMS.Core.Enums;
using GMS.Core.Services;

namespace GMS.Tests;

/// <summary>
/// The warning a tenant's administrators get shortly before their subscription runs out.
/// </summary>
/// <remarks>
/// Raised at sign-in rather than on a timer, because there is no scheduler in this system. The
/// cases worth pinning are the silences: too early, already expired, never expires, and raised
/// twice. A reminder that fires on every single sign-in would train people to ignore it.
/// </remarks>
public class ExpiryWarningTests
{
    /// <summary>Puts the organisation a given number of days from the end.</summary>
    private static GMS.Core.Models.Organization GivenExpiryIn(TestHost host, double days, string code)
    {
        var org = host.NewTenant(code);
        host.User.IsPlatformOperator = true;
        var ends = days == double.PositiveInfinity ? (DateTime?)null : host.Clock.UtcNow.AddDays(days);
        Assert.True(host.Get<SubscriptionService>()
            .SetSubscription(org.Id, "basic", host.Clock.UtcNow.AddDays(-10), ends).Succeeded);
        host.User.IsPlatformOperator = false;
        host.User.AllPermissions = true;
        return host.Get<OrganizationService>() is var _ ? org : org;
    }

    private static int WarnedCount(TestHost host, int orgId)
    {
        host.User.IsPlatformOperator = true;
        return host.Get<SubscriptionService>().WarnIfExpiringSoon(orgId);
    }

    [Fact]
    public void Two_days_out_the_administrator_is_warned()
    {
        using var host = new TestHost().WithBaseline();
        var org = GivenExpiryIn(host, 1.5, "soon");

        Assert.Equal(1, WarnedCount(host, org.Id));
    }

    [Fact]
    public void A_week_out_nobody_is_warned_yet()
    {
        using var host = new TestHost().WithBaseline();
        var org = GivenExpiryIn(host, 7, "later");

        Assert.Equal(0, WarnedCount(host, org.Id));
    }

    [Fact]
    public void An_organisation_that_never_expires_is_never_warned()
    {
        using var host = new TestHost().WithBaseline();
        var org = GivenExpiryIn(host, double.PositiveInfinity, "immortal");

        Assert.Equal(0, WarnedCount(host, org.Id));
    }

    [Fact]
    public void An_already_expired_organisation_is_not_told_it_is_about_to_expire()
    {
        using var host = new TestHost().WithBaseline();
        var org = GivenExpiryIn(host, 1, "gone");
        host.Clock.AdvanceDays(3);

        Assert.Equal(0, WarnedCount(host, org.Id));
    }

    [Fact]
    public void The_same_deadline_is_only_ever_raised_once()
    {
        using var host = new TestHost().WithBaseline();
        var org = GivenExpiryIn(host, 1.5, "repeat");

        Assert.Equal(1, WarnedCount(host, org.Id));
        Assert.Equal(0, WarnedCount(host, org.Id));
        Assert.Equal(0, WarnedCount(host, org.Id));
    }

    [Fact]
    public void Moving_the_deadline_to_a_different_day_raises_a_fresh_warning()
    {
        // A different date is genuinely different news, so it is worth saying again.
        using var host = new TestHost().WithBaseline();
        var org = GivenExpiryIn(host, 1.5, "moved");      // ends tomorrow
        Assert.Equal(1, WarnedCount(host, org.Id));

        // Brought forward to today - still inside the warning window, but a different day.
        host.User.IsPlatformOperator = true;
        Assert.True(host.Get<SubscriptionService>()
            .SetSubscription(org.Id, "basic", host.Clock.UtcNow.AddDays(-10), host.Clock.UtcNow.AddHours(12)).Succeeded);

        Assert.Equal(1, WarnedCount(host, org.Id));
    }

    [Fact]
    public void Pushing_the_end_date_beyond_the_window_stops_the_warnings()
    {
        // The useful consequence of keying on the date: renew them and the nagging stops.
        using var host = new TestHost().WithBaseline();
        var org = GivenExpiryIn(host, 1.5, "renewed");
        Assert.Equal(1, WarnedCount(host, org.Id));

        host.User.IsPlatformOperator = true;
        Assert.True(host.Get<SubscriptionService>().ExtendBy(org.Id, 30).Succeeded);

        Assert.Equal(0, WarnedCount(host, org.Id));
    }

    [Fact]
    public void The_warning_reaches_the_administrator_and_says_when()
    {
        using var host = new TestHost().WithBaseline();
        var org = GivenExpiryIn(host, 1.5, "wording");
        WarnedCount(host, org.Id);

        // Read as the tenant's administrator would.
        host.User.IsPlatformOperator = false;
        host.User.AllPermissions = true;
        host.Tenant.OrganizationId = org.Id;
        var admin = host.Get<OrganizationService>();
        host.User.IsPlatformOperator = true;
        var adminUser = admin.ListUsers(org.Id).Value.Single();
        host.User.IsPlatformOperator = false;
        host.User.UserId = adminUser.Id;

        var mine = host.Get<NotificationService>().ListForCurrentUser();
        var warning = Assert.Single(mine, n => n.DedupeKey.StartsWith("subscription-expiry"));

        Assert.Equal(NotificationSeverity.Warning, warning.Severity);
        Assert.Contains("about to end", warning.Title);
        Assert.Contains("locked out", warning.Message);
    }

    [Fact]
    public void Signing_in_is_what_raises_it()
    {
        using var host = new TestHost().WithBaseline();
        var org = GivenExpiryIn(host, 1.5, "atlogin");

        // Give the administrator a password we know, the way the operator would.
        host.User.IsPlatformOperator = true;
        var adminUser = host.Get<OrganizationService>().ListUsers(org.Id).Value.Single();
        Assert.True(host.Get<OrganizationService>()
            .ResetUserPassword(org.Id, adminUser.Id, "SignIn#2026").Succeeded);
        host.User.IsPlatformOperator = false;

        var signIn = host.Get<AuthService>().SignInWithTenant(adminUser.UserName, "SignIn#2026", "atlogin");
        Assert.True(signIn.Succeeded, signIn.ErrorMessage);

        host.User.UserId = adminUser.Id;
        host.Tenant.OrganizationId = org.Id;
        host.User.AllPermissions = true;
        var mine = host.Get<NotificationService>().ListForCurrentUser();
        Assert.Contains(mine, n => n.DedupeKey.StartsWith("subscription-expiry"));
    }
}
