using GMS.Core.Abstractions;
using GMS.Core.Common;
using GMS.Core.Contracts;
using GMS.Core.Enums;
using GMS.Core.Models;
using GMS.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GMS.Web.Pages.Inventory;

[Authorize("perm:inventory.view")]
public class IndexModel(
    InventoryService inventory,
    ProductService products,
    ShopService shops,
    ICurrentUser me) : PageModel
{
    [BindProperty(SupportsGet = true)] public int? ProductId { get; set; }

    public List<SelectListItem> ProductOptions { get; private set; } = new();
    public Product? Selected { get; private set; }
    public PagedResult<StockLedgerRow>? Ledger { get; private set; }

    /// <summary>
    /// Where the selected product's stock is sitting. Empty until the organisation has a shop,
    /// which is when the question starts to have more than one answer.
    /// </summary>
    public IReadOnlyList<ShopStockRow> Distribution { get; private set; } = Array.Empty<ShopStockRow>();

    /// <summary>Where an adjustment applies. A pinned attendant has no choice; it is their shop.</summary>
    public List<SelectListItem> LocationOptions { get; private set; } = new();
    public bool HasShops { get; private set; }
    public bool IsPinned => me.ShopId is not null;

    [BindProperty] public string Direction { get; set; } = "In";
    [BindProperty] public decimal Qty { get; set; } = 1;
    [BindProperty] public string? Note { get; set; }
    [BindProperty] public string? AdjustAt { get; set; }

    public void OnGet() => Load();

    public IActionResult OnPostAdjust()
    {
        if (ProductId is not int pid) return RedirectToPage();
        var dir = Direction == "Out" ? StockMovementDirection.Out : StockMovementDirection.In;
        // "" is central; the service forces an attendant back to their own shop regardless.
        int? at = string.IsNullOrEmpty(AdjustAt) ? null : int.Parse(AdjustAt);
        var result = inventory.Adjust(pid, dir, Qty, Note ?? "", at);
        TempData["Flash"] = result.Succeeded ? "Stock adjusted." : result.ErrorMessage;
        return RedirectToPage(new { productId = pid });
    }

    private void Load()
    {
        // Load only active products, limited to a reasonable number for the dropdown (100 should be more than enough)
        var ps = products.Search(new QueryOptions { PageSize = 100, SortBy = "name" }, activeOnly: true);
        if (ps.Succeeded)
            ProductOptions = ps.Value.Items.Select(p => new SelectListItem($"{p.Sku} — {p.Name}", p.Id.ToString())).ToList();

        var sl = shops.List();
        var shopList = sl.Succeeded ? sl.Value : Array.Empty<Shop>();
        HasShops = shopList.Count > 0;

        LocationOptions = new();
        if (!IsPinned) LocationOptions.Add(new SelectListItem("Central", ""));
        LocationOptions.AddRange(shopList.Select(s => new SelectListItem(s.Name, s.Id.ToString())));

        if (ProductId is not int pid) return;

        var one = products.GetById(pid);
        if (one.Succeeded) Selected = one.Value;

        var led = inventory.GetLedger(pid, new QueryOptions { PageSize = 100 });
        if (led.Succeeded) Ledger = led.Value;

        if (!HasShops) return;
        var spread = inventory.GetDistribution(pid);
        if (spread.Succeeded) Distribution = spread.Value;
    }
}
