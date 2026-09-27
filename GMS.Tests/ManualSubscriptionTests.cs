using GMS.Core.Services;

namespace GMS.Tests;

/// <summary>
/// The operator setting a subscription by hand.
/// </summary>
/// <remarks>
/// Payment happens outside the system for now, so the operator records the outcome: a plan, a start
/// date and an end date, or no end at all. These pin the parts that are easy to get subtly wrong -
/// that "never expires" really never locks anyone out, that a stale trial date cannot override a
/// deliberate expiry, and that extending a customer who lapsed months ago gives them the days
/// rather than leaving them expired.
/// </remarks>
public class ManualSubscriptionTests
{
    [Fact]
    public void The_operator_can_set_a_plan_with_a_start_and_end_date()
    {
        using var host = new TestHost().WithBaseline();
        var org = host.NewTenant("paying");
        host.User.IsPlatformOperator = true;

        var starts = host.Clock.UtcNow;
        var ends = starts.AddDays(365);
        Assert.True(host.Get<SubscriptionService>().SetSubscription(org.Id, "professional", starts, ends).Succeeded);

        var after = host.Get<OrganizationService>().GetById(org.Id).Value;
        Assert.Equal("professional", after.Plan);
        Assert.Equal(ends, after.SubscriptionEndsAtUtc);
        Assert.False(OrganizationService.IsLapsed(after, host.Clock.UtcNow));
    }

    [Fact]
    public void Setting_no_end_date_means_it_never_lapses()
    {
        using var host = new TestHost().WithBaseline();
        var org = host.NewTenant("forever");
        host.User.IsPlatformOperator = true;

        Assert.True(host.Get<SubscriptionService>()
            .SetSubscription(org.Id, "enterprise", host.Clock.UtcNow, null).Succeeded);

        var after = host.Get<OrganizationService>().GetById(org.Id).Value;
        Assert.Null(after.SubscriptionEndsAtUtc);
        Assert.Null(after.TrialEndsAtUtc);

        host.Clock.AdvanceDays(365 * 20);
        Assert.False(OrganizationService.IsLapsed(after, host.Clock.UtcNow));
    }

    [Fact]
    public void Setting_a_subscription_clears_the_old_trial_date()
    {
        // Otherwise a trial date still in the future would override the end date just set,
        // and the organisation would stay entitled past its own expiry.
        using var host = new TestHost().WithBaseline();
        var org = host.NewTenant("converting");
        Assert.NotNull(org.TrialEndsAtUtc);
        host.User.IsPlatformOperator = true;

        var ends = host.Clock.UtcNow.AddDays(1);
        Assert.True(host.Get<SubscriptionService>().SetSubscription(org.Id, "basic", host.Clock.UtcNow, ends).Succeeded);

        var after = host.Get<OrganizationService>().GetById(org.Id).Value;
        Assert.Null(after.TrialEndsAtUtc);

        host.Clock.AdvanceDays(2);
        Assert.True(OrganizationService.IsLapsed(after, host.Clock.UtcNow));
    }

    [Fact]
    public void An_end_date_before_the_start_is_refused()
    {
        using var host = new TestHost().WithBaseline();
        var org = host.NewTenant("backwards");
        host.User.IsPlatformOperator = true;

        var result = host.Get<SubscriptionService>()
            .SetSubscription(org.Id, "basic", host.Clock.UtcNow, host.Clock.UtcNow.AddDays(-1));

        Assert.True(result.Failed);
        Assert.Contains("after the start", result.ErrorMessage);
    }

    [Fact]
    public void A_blank_plan_is_refused()
    {
        using var host = new TestHost().WithBaseline();
        var org = host.NewTenant("noplan");
        host.User.IsPlatformOperator = true;

        Assert.True(host.Get<SubscriptionService>()
            .SetSubscription(org.Id, "  ", host.Clock.UtcNow, host.Clock.UtcNow.AddDays(30)).Failed);
    }

    [Fact]
    public void Adding_days_extends_from_the_existing_end_when_still_live()
    {
        using var host = new TestHost().WithBaseline();
        var org = host.NewTenant("extending");
        host.User.IsPlatformOperator = true;
        var ends = host.Clock.UtcNow.AddDays(10);
        host.Get<SubscriptionService>().SetSubscription(org.Id, "basic", host.Clock.UtcNow, ends);

        Assert.True(host.Get<SubscriptionService>().ExtendBy(org.Id, 30).Succeeded);

        var after = host.Get<OrganizationService>().GetById(org.Id).Value;
        Assert.Equal(ends.AddDays(30), after.SubscriptionEndsAtUtc);
    }

    [Fact]
    public void Adding_days_to_a_lapsed_customer_counts_from_today()
    {
        // Thirty days added to someone two months expired should give them thirty days, not
        // leave them expired.
        using var host = new TestHost().WithBaseline();
        var org = host.NewTenant("longgone");
        host.User.IsPlatformOperator = true;
        host.Get<SubscriptionService>().SetSubscription(org.Id, "basic", host.Clock.UtcNow, host.Clock.UtcNow.AddDays(5));
        host.Clock.AdvanceDays(65);
        Assert.True(OrganizationService.IsLapsed(host.Get<OrganizationService>().GetById(org.Id).Value, host.Clock.UtcNow));

        Assert.True(host.Get<SubscriptionService>().ExtendBy(org.Id, 30).Succeeded);

        var after = host.Get<OrganizationService>().GetById(org.Id).Value;
        Assert.Equal(host.Clock.UtcNow.AddDays(30), after.SubscriptionEndsAtUtc);
        Assert.False(OrganizationService.IsLapsed(after, host.Clock.UtcNow));
    }

    [Fact]
    public void Extending_something_that_never_expires_is_refused_rather_than_given_an_end_date()
    {
        using var host = new TestHost().WithBaseline();
        var org = host.NewTenant("immortal");
        host.User.IsPlatformOperator = true;
        host.Get<SubscriptionService>().SetSubscription(org.Id, "enterprise", host.Clock.UtcNow, null);

        var result = host.Get<SubscriptionService>().ExtendBy(org.Id, 30);

        Assert.True(result.Failed);
        Assert.Contains("never to expire", result.ErrorMessage);
        Assert.Null(host.Get<OrganizationService>().GetById(org.Id).Value.SubscriptionEndsAtUtc);
    }

    [Fact]
    public void A_tenant_admin_cannot_set_their_own_subscription()
    {
        using var host = new TestHost().WithBaseline();
        var org = host.NewTenant("cheeky");   // leaves the caller as tenant admin, all permissions

        Assert.True(host.Get<SubscriptionService>()
            .SetSubscription(org.Id, "enterprise", host.Clock.UtcNow, null).Failed);
        Assert.True(host.Get<SubscriptionService>().ExtendBy(org.Id, 3650).Failed);
    }
}
