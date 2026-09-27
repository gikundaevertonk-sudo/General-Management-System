using GMS.Core.Models;
using GMS.Core.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GMS.Operator.Pages;

[Authorize]
public class IndexModel(OrganizationService organizations) : PageModel
{
    public IReadOnlyList<Organization> Organizations { get; private set; } = [];
    public string? Error { get; private set; }

    /// <summary>Counts per organisation, so a dead or unused tenant is visible at a glance.</summary>
    private Dictionary<int, TenantStats> _stats = [];

    /// <summary>
    /// Whether the counts were actually read. Distinguishes "this organisation has nothing in it"
    /// from "we could not find out", which would otherwise look identical: ListStats groups rows,
    /// so an organisation with none simply has no entry, and a failed read leaves every entry
    /// missing. Reporting every tenant as broken because one query failed would be worse than
    /// saying nothing.
    /// </summary>
    private bool _statsLoaded;

    public string UsageOf(Organization o)
    {
        if (!_statsLoaded) return $"max {o.MaxUsers}";

        // No entry means no rows at all, which for users means onboarding never finished -
        // nobody can sign in to it.
        var s = _stats.GetValueOrDefault(o.Id) ?? new TenantStats();
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

        // Five grouped queries for the whole list, not five per row. Calling GetStats in a loop
        // here meant fifty customers cost two hundred and fifty round trips to render one page.
        var stats = organizations.ListStats();
        _statsLoaded = stats.Succeeded;
        if (stats.Succeeded) _stats = stats.Value;
    }
}
