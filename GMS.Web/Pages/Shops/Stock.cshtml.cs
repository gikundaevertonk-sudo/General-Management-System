using GMS.Core.Abstractions;
using GMS.Core.Common;
using GMS.Core.Contracts;
using GMS.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GMS.Web.Pages.Shops;

/// <summary>
/// What one location is holding, and the form a manager uses to send stock somewhere else.
/// </summary>
/// <remarks>
/// Central is this page with no <c>shopId</c>, rather than a page of its own: it is a stock
/// location like any other, and giving it the same screen means allocating out of it and out of a
/// branch are the same operation with the same wording.
/// </remarks>
[Authorize("perm:inventory.view")]
public class StockModel(
    InventoryService inventory,
    ShopService shops,
    ProductService products,
    ICurrentUser me) : PageModel
{
    [BindProperty(SupportsGet = true)] public int? ShopId { get; set; }
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public int PageNo { get; set; } = 1;

    public ShopSummaryRow? Summary { get; private set; }
    public PagedResult<ShopStockRow>? Held { get; private set; }

    /// <summary>Where stock from here may be sent: central plus every other open shop.</summary>
    public List<SelectListItem> DestinationOptions { get; private set; } = new();
    public List<SelectListItem> ProductOptions { get; private set; } = new();

    // allocate form
    [BindProperty] public int AllocateProductId { get; set; }
    [BindProperty] public string? AllocateTo { get; set; }
    [BindProperty] public decimal AllocateQty { get; set; } = 1;
    [BindProperty] public string? AllocateNote { get; set; }

    public bool CanAllocate => me.HasPermission(GMS.Core.Security.PermissionCodes.Shops.Allocate);

    public void OnGet() => Load();

    public IActionResult OnPostAllocate()
    {
        // "" is central, which is why the destination is posted as a string: an int? bound from an
        // empty <option> is indistinguishable from one that was never submitted.
        int? destination = string.IsNullOrEmpty(AllocateTo) ? null : int.Parse(AllocateTo);

        var result = inventory.Allocate(AllocateProductId, ShopId, destination, AllocateQty, AllocateNote ?? "");
        TempData["Flash"] = result.Succeeded ? "Stock allocated." : result.ErrorMessage;
        return RedirectToPage(new { shopId = ShopId, q = Q, pageNo = PageNo });
    }

    private void Load()
    {
        var summary = inventory.GetSummaryAt(ShopId);
        if (summary.Succeeded) Summary = summary.Value;

        var held = inventory.GetStockAt(ShopId, new QueryOptions { Search = Q ?? "", Page = PageNo, PageSize = 25 });
        if (held.Succeeded) Held = held.Value;

        if (!CanAllocate) return;

        var ps = products.Search(new QueryOptions { PageSize = QueryOptions.MaxPageSize, SortBy = "name" }, activeOnly: true);
        if (ps.Succeeded)
            ProductOptions = ps.Value.Items.Select(p => new SelectListItem($"{p.Sku} — {p.Name}", p.Id.ToString())).ToList();

        DestinationOptions = new();
        if (ShopId is not null) DestinationOptions.Add(new SelectListItem("Central", ""));

        var list = shops.List();
        if (!list.Succeeded) return;
        foreach (var shop in list.Value.Where(s => s.Id != ShopId))
            DestinationOptions.Add(new SelectListItem(shop.Name, shop.Id.ToString()));
    }
}
