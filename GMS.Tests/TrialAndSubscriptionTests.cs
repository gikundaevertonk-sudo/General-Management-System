using GMS.Core.Models;
using GMS.Core.Services;

namespace GMS.Tests;

/// <summary>
/// Whether an organisation is still entitled to be let in.
/// </summary>
/// <remarks>
/// This is the rule that decides whether a paying customer can work, so the edge cases matter
/// more than the happy path. IsLapsed is deliberately "neither date is still in the future":
/// an earlier version required a trial end date in the past, which meant a customer who had
/// paid - and so had no trial date at all - could never be locked out however long their
/// subscription had been expired.
/// </remarks>
public class TrialAndSubscriptionTests
{
    private static readonly DateTime Now = new(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void An_organisation_with_no_dates_at_all_is_not_lapsed()
    {
        // Nothing has been promised, so there is nothing to have expired. Treating this as
        // lapsed would lock out a fresh install before anyone had configured it.
        Assert.False(OrganizationService.IsLapsed(new Organization(), Now));
    }

    [Fact]
    public void A_trial_still_running_is_not_lapsed()
    {
        var org = new Organization { TrialEndsAtUtc = Now.AddDays(3) };
        Assert.False(OrganizationService.IsLapsed(org, Now));
    }

    [Fact]
    public void A_trial_that_has_run_out_is_lapsed()
    {
        var org = new Organization { TrialEndsAtUtc = Now.AddDays(-1) };
        Assert.True(OrganizationService.IsLapsed(org, Now));
    }

    [Fact]
    public void A_paid_customer_with_no_trial_date_is_locked_out_once_the_subscription_expires()
    {
        // The regression this rule exists for.
        var org = new Organization { TrialEndsAtUtc = null, SubscriptionEndsAtUtc = Now.AddDays(-30) };
        Assert.True(OrganizationService.IsLapsed(org, Now));
    }

    [Fact]
    public void A_live_subscription_carries_an_expired_trial()
    {
        // Converting from trial to paid must not be held against them.
        var org = new Organization { TrialEndsAtUtc = Now.AddDays(-10), SubscriptionEndsAtUtc = Now.AddDays(20) };
        Assert.False(OrganizationService.IsLapsed(org, Now));
    }

    [Fact]
    public void A_live_trial_carries_an_expired_subscription()
    {
        var org = new Organization { TrialEndsAtUtc = Now.AddDays(5), SubscriptionEndsAtUtc = Now.AddDays(-5) };
        Assert.False(OrganizationService.IsLapsed(org, Now));
    }

    [Fact]
    public void Expiry_is_inclusive_of_the_moment_itself()
    {
        // Exactly on the boundary counts as still valid - an off-by-one here locks a customer
        // out a day early.
        var org = new Organization { SubscriptionEndsAtUtc = Now };
        Assert.False(OrganizationService.IsLapsed(org, Now));
        Assert.True(OrganizationService.IsLapsed(org, Now.AddTicks(1)));
    }

    [Fact]
    public void A_new_tenant_starts_on_a_trial_that_has_not_lapsed()
    {
        using var host = new TestHost().WithBaseline();
        var org = host.NewTenant("fresh");

        Assert.NotNull(org.TrialEndsAtUtc);
        Assert.False(OrganizationService.IsLapsed(org, host.Clock.UtcNow));
        Assert.True(org.IsActive);
    }

    [Fact]
    public void A_trial_lapses_once_enough_time_passes()
    {
        using var host = new TestHost().WithBaseline();
        var org = host.NewTenant("ticking");

        host.Clock.AdvanceDays(31);

        Assert.True(OrganizationService.IsLapsed(org, host.Clock.UtcNow));
    }

    [Fact]
    public void Renewing_clears_a_lapse()
    {
        using var host = new TestHost().WithBaseline();
        var org = host.NewTenant("renewme");
        host.Clock.AdvanceDays(31);
        Assert.True(OrganizationService.IsLapsed(org, host.Clock.UtcNow));

        host.User.IsPlatformOperator = true;
        Assert.True(host.Get<SubscriptionService>().Renew(org.Id).Succeeded);

        var after = host.Get<OrganizationService>().GetById(org.Id).Value;
        Assert.False(OrganizationService.IsLapsed(after, host.Clock.UtcNow));
    }

    [Fact]
    public void Marking_unpaid_locks_the_organisation_out_without_suspending_it()
    {
        using var host = new TestHost().WithBaseline();
        var org = host.NewTenant("unpaid");

        host.User.IsPlatformOperator = true;
        Assert.True(host.Get<SubscriptionService>().ExpireSubscription(org.Id).Succeeded);

        var after = host.Get<OrganizationService>().GetById(org.Id).Value;
        Assert.True(OrganizationService.IsLapsed(after, host.Clock.UtcNow));
        // Still active: a billing lapse and an administrative block are different states, and
        // the console presents them differently.
        Assert.True(after.IsActive);
    }

    [Fact]
    public void Suspending_is_not_the_same_as_lapsing()
    {
        using var host = new TestHost().WithBaseline();
        var org = host.NewTenant("blocked");

        host.User.IsPlatformOperator = true;
        Assert.True(host.Get<OrganizationService>().Suspend(org.Id).Succeeded);

        var after = host.Get<OrganizationService>().GetById(org.Id).Value;
        Assert.False(after.IsActive);
        Assert.False(OrganizationService.IsLapsed(after, host.Clock.UtcNow));
    }
}
