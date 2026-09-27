using System.ComponentModel.DataAnnotations;
using GMS.Core.Models;
using GMS.Core.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GMS.Operator.Pages;

[Authorize]
public class OrgModel(OrganizationService organizations, SubscriptionService subscriptions) : PageModel
{
    [BindProperty(SupportsGet = true)] public int Id { get; set; }

    public Organization? Org { get; private set; }
    public Subscription? Subscription { get; private set; }
    public TenantStats? Stats { get; private set; }
    public IReadOnlyList<TenantUser> Users { get; private set; } = [];
    public string? Error { get; private set; }

    // edit form. The user limit is deliberately not here - it has its own form beside the user
    // list, so editing a name cannot carry a stale seat count along with it.
    [BindProperty] public string Name { get; set; } = "";
    [BindProperty] public string? Email { get; set; }

    // seats
    [BindProperty] public int SeatLimit { get; set; }

    // reset form
    [BindProperty] public int ResetUserId { get; set; }
    [BindProperty] public string? TempPassword { get; set; }

    // subscription form. NeverExpires wins over EndsOn when both are posted - an operator who
    // ticks the box has said what they mean more clearly than a date left in the field.
    [BindProperty] public string? Plan { get; set; }
    [BindProperty, DataType(DataType.Date)] public DateTime StartsOn { get; set; }
    [BindProperty, DataType(DataType.Date)] public DateTime? EndsOn { get; set; }
    [BindProperty] public bool NeverExpires { get; set; }
    [BindProperty] public int ExtendDays { get; set; } = 30;

    /// <summary>The plans offered in the dropdown. Free text is still accepted on post.</summary>
    public static readonly string[] Plans = ["trial", "basic", "professional", "enterprise"];

    // delete confirmation
    [BindProperty] public string? ConfirmCode { get; set; }

    public bool IsLapsed => Org is not null && OrganizationService.IsLapsed(Org, DateTime.UtcNow);

    public bool IsDefault =>
        Org?.Code?.Equals(DataSeeder.DefaultOrganizationCode, StringComparison.OrdinalIgnoreCase) == true;

    public IActionResult OnGet()
    {
        if (!Load()) return NotFound();
        FillForm();
        return Page();
    }

    public IActionResult OnPostDetails()
    {
        // Loaded first because the existing limit is passed straight back through: seats are set by
        // their own form, and this one must not be able to change them.
        if (!Load()) return NotFound();
        var r = organizations.UpdateDetails(Id, Name, Email ?? "", Org!.MaxUsers);
        // keepForm: this is the one handler whose posted values belong in the form afterwards,
        // so a rejected edit comes back for correcting instead of being silently reverted.
        return Finish(r.Succeeded, "Details saved.", r.ErrorMessage, keepForm: true);
    }

    public IActionResult OnPostSeats()
    {
        var r = organizations.SetUserLimit(Id, SeatLimit);
        if (!r.Succeeded) return Finish(false, "", r.ErrorMessage);

        var inUse = r.Value;
        var message = inUse > SeatLimit
            ? $"Limit set to {SeatLimit}. They currently have {inUse} users, so {inUse - SeatLimit} " +
              "must be removed before anyone new can be added. Nobody has been locked out."
            : $"Limit set to {SeatLimit}. {inUse} in use, {SeatLimit - inUse} spare.";
        return Finish(true, message, null);
    }

    public IActionResult OnPostReset()
    {
        // Without this the service answers "User not found", which reads as a broken account
        // rather than a forgotten click on Reset password.
        if (ResetUserId == 0) return Finish(false, "", "Pick a user from the list above first.");

        var r = organizations.ResetUserPassword(Id, ResetUserId, TempPassword ?? "");
        // The password itself is deliberately not echoed back: the operator typed it, so
        // repeating it buys nothing and would put it in the flash cookie in readable form.
        return Finish(r.Succeeded,
            "Password reset. They must change it the next time they sign in.", r.ErrorMessage);
    }

    public IActionResult OnPostSubscription()
    {
        // The tick box is authoritative, so a date left over in the field cannot contradict it.
        var ends = NeverExpires ? (DateTime?)null : EndsOn;
        if (!NeverExpires && ends is null)
            return Finish(false, "", "Give an end date, or tick that it never expires.", keepForm: true);

        var r = subscriptions.SetSubscription(Id, Plan ?? "", DateTime.SpecifyKind(StartsOn, DateTimeKind.Utc),
                                              ends is null ? null : DateTime.SpecifyKind(ends.Value, DateTimeKind.Utc));
        var message = ends is null
            ? "Subscription saved. This organisation never expires."
            : $"Subscription saved. It runs to {ends:d MMMM yyyy}.";
        return Finish(r.Succeeded, message, r.ErrorMessage, keepForm: true);
    }

    public IActionResult OnPostExtend()
    {
        var r = subscriptions.ExtendBy(Id, ExtendDays);
        return Finish(r.Succeeded, $"Added {ExtendDays} days.", r.ErrorMessage);
    }

    public IActionResult OnPostRenew()
    {
        var r = subscriptions.Renew(Id);
        return Finish(r.Succeeded, "Subscription renewed for 30 days.", r.ErrorMessage);
    }

    public IActionResult OnPostExpire()
    {
        var r = subscriptions.ExpireSubscription(Id);
        return Finish(r.Succeeded, "Marked unpaid. Its users are locked out until renewed.", r.ErrorMessage);
    }

    public IActionResult OnPostSuspend()
    {
        var r = organizations.Suspend(Id);
        return Finish(r.Succeeded, "Organisation suspended.", r.ErrorMessage);
    }

    public IActionResult OnPostActivate()
    {
        var r = organizations.Activate(Id);
        return Finish(r.Succeeded, "Organisation reactivated.", r.ErrorMessage);
    }

    public IActionResult OnPostDelete()
    {
        if (!Load()) return NotFound();
        // Typing the code is the guard. A misclick cannot destroy a tenant; only deliberately
        // writing out its name can. Compared against the loaded code, never against a null one,
        // so an empty box can never match.
        if (!string.Equals(ConfirmCode?.Trim(), Org!.Code, StringComparison.OrdinalIgnoreCase))
            return Finish(false, "", "Type the organisation code exactly to confirm deletion.");

        var name = Org.Name;
        var r = organizations.DeleteOrganization(Id);
        if (!r.Succeeded) return Finish(false, "", r.ErrorMessage);

        TempData["Flash"] = $"{name} and all of its data have been deleted.";
        return RedirectToPage("/Index");
    }

    private IActionResult Finish(bool ok, string success, string? failure, bool keepForm = false)
    {
        if (ok)
        {
            TempData["Flash"] = success;
            return RedirectToPage(new { id = Id });
        }
        Error = failure ?? "That did not work.";
        if (Org is null) Load();
        // Every form on the page posts to this one page model, so a failed password reset or
        // suspend arrives with the details fields unbound - blank name and email, a cap of zero.
        // Refilling them from the stored organisation stops that form rendering the empty shape
        // of whichever other form was actually submitted.
        if (!keepForm) FillForm();
        return Page();
    }

    private void FillForm()
    {
        if (Org is null) return;
        Name = Org.Name ?? "";
        Email = Org.Email;
        SeatLimit = Org.MaxUsers;

        Plan = Org.Plan;
        // Whichever end date is in force, so the field shows what is actually being enforced
        // rather than whichever column happens to be populated.
        var ends = Org.SubscriptionEndsAtUtc ?? Org.TrialEndsAtUtc;
        NeverExpires = ends is null;
        EndsOn = ends?.Date;
        StartsOn = (Subscription?.BillingCycleStartAtUtc ?? Org.CreatedAtUtc).Date;
    }

    private bool Load()
    {
        var o = organizations.GetById(Id);
        if (o.Failed) return false;
        Org = o.Value;

        var s = organizations.GetStats(Id);
        if (s.Succeeded) Stats = s.Value;

        var u = organizations.ListUsers(Id);
        if (u.Succeeded) Users = u.Value;

        var sub = subscriptions.GetByOrganizationId(Id);
        if (sub.Succeeded) Subscription = sub.Value;
        return true;
    }
}
