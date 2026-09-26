using GMS.Core.Models;
using GMS.Core.Services;
using GMS.Web.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GMS.Web.Pages.Platform;

[Authorize(AuthenticationSchemes = PlatformAuth.Scheme, Policy = PlatformAuth.Policy)]
public class IndexModel(OrganizationService organizations, SubscriptionService subscriptions) : PageModel
{
    public IReadOnlyList<Organization> Organizations { get; private set; } = [];
    public string? Error { get; private set; }

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

    public IActionResult OnPostSuspend(int id)
    {
        var r = organizations.Suspend(id);
        Flash(r.Succeeded, "Organisation suspended. Its users are now blocked.", r.ErrorMessage);
        return RedirectToPage();
    }

    public IActionResult OnPostActivate(int id)
    {
        var r = organizations.Activate(id);
        Flash(r.Succeeded, "Organisation reactivated.", r.ErrorMessage);
        return RedirectToPage();
    }

    public IActionResult OnPostRenew(int id)
    {
        var r = subscriptions.Renew(id);
        Flash(r.Succeeded, "Subscription renewed for 30 days.", r.ErrorMessage);
        return RedirectToPage();
    }

    public IActionResult OnPostExpire(int id)
    {
        var r = subscriptions.ExpireSubscription(id);
        Flash(r.Succeeded, "Subscription marked unpaid. The organisation is locked out until renewed.", r.ErrorMessage);
        return RedirectToPage();
    }

    private void Flash(bool ok, string success, string? failure)
    {
        if (ok) TempData["Flash"] = success;
        else TempData["Error"] = failure ?? "That did not work.";
    }

    private void Load()
    {
        var result = organizations.ListAll();
        if (result.Succeeded) Organizations = result.Value;
        else Error = result.ErrorMessage;
    }
}
