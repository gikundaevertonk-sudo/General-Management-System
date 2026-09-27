using GMS.Core.Models;
using GMS.Core.Services;
using GMS.Web.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GMS.Web.Pages.Platform;

[Authorize(AuthenticationSchemes = PlatformAuth.Scheme, Policy = PlatformAuth.Policy)]
public class IndexModel(OrganizationService organizations) : PageModel
{
    public IReadOnlyList<Organization> Organizations { get; private set; } = [];
    public string? Error { get; private set; }

    /// <summary>Counts per organisation, so a dead or unused tenant is visible at a glance.</summary>
    private readonly Dictionary<int, TenantStats> _stats = [];

    public string UsageOf(Organization o)
    {
        if (!_stats.TryGetValue(o.Id, out var s)) return $"max {o.MaxUsers}";
        // No users means onboarding never finished - nobody can sign in to it at all.
        if (s.UserCount == 0) return "no users — broken";
        return $"{s.UserCount}/{o.MaxUsers} users · {s.TransactionCount} txns";
    }

    public int TotalCount => Organizations.Count;
    public int ActiveCount => Organizations.Count(o => o.IsActive && !IsLapsed(o));
    public int SuspendedCount => Organizations.Count(o => !o.IsActive);
    public int LapsedCount => Organizations.Count(o => o.IsActive && IsLapsed(o));

    /// <summary>Defers to the rule TrialExpiryMiddleware enforces, so the two cannot drift.</summary>
    public static bool IsLapsed(Organization org) =>
        OrganizationService.IsLapsed(org, DateTime.UtcNow);

    public static string StatusOf(Organization org) =>
        !org.IsActive ? "Suspended" : IsLapsed(org) ? "Not renewed" : "Active";

    public void OnGet() => Load();

    private void Load()
    {
        var result = organizations.ListAll();
        if (result.Succeeded) Organizations = result.Value;
        else { Error = result.ErrorMessage; return; }

        // One round of counts per organisation. Fine at this scale - this is the tenant list,
        // not a customer-facing page, and there are as many rows as you have customers.
        _stats.Clear();
        foreach (var o in Organizations)
        {
            var s = organizations.GetStats(o.Id);
            if (s.Succeeded) _stats[o.Id] = s.Value;
        }
    }
}
