using GMS.Core.Abstractions;
using GMS.Core.Contracts;
using GMS.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GMS.Web.Pages.Shops;

/// <summary>
/// The central point: every shop the organisation runs, with what each one is holding.
/// </summary>
[Authorize("perm:shops.view")]
public class IndexModel(ShopService shops, InventoryService inventory, ICurrentUser me) : PageModel
{
    public IReadOnlyList<ShopSummaryRow> Rows { get; private set; } = Array.Empty<ShopSummaryRow>();

    /// <summary>
    /// What has not been sent out to a shop yet. Null for a user pinned to one shop, who has no
    /// view of the central pool at all.
    /// </summary>
    public ShopSummaryRow? Central { get; private set; }

    public bool IsPinned => me.ShopId is not null;

    public void OnGet()
    {
        var overview = shops.GetOverview();
        if (overview.Succeeded) Rows = overview.Value;

        if (IsPinned) return;
        var central = inventory.GetSummaryAt(null);
        if (central.Succeeded) Central = central.Value;
    }

    public IActionResult OnPostToggle(int id, bool active)
    {
        var result = shops.SetActive(id, !active);
        TempData["Flash"] = result.Succeeded
            ? (active ? "Shop closed." : "Shop reopened.")
            : result.ErrorMessage;
        return RedirectToPage();
    }
}
