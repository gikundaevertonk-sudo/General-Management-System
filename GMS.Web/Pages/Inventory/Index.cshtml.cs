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
public class IndexModel(InventoryService inventory, ProductService products) : PageModel
{
    [BindProperty(SupportsGet = true)] public int? ProductId { get; set; }

    public List<SelectListItem> ProductOptions { get; private set; } = new();
    public Product? Selected { get; private set; }
    public PagedResult<StockLedgerRow>? Ledger { get; private set; }

    [BindProperty] public string Direction { get; set; } = "In";
    [BindProperty] public decimal Qty { get; set; } = 1;
    [BindProperty] public string? Note { get; set; }

    public void OnGet() => Load();

    public IActionResult OnPostAdjust()
    {
        if (ProductId is not int pid) return RedirectToPage();
        var dir = Direction == "Out" ? StockMovementDirection.Out : StockMovementDirection.In;
        var result = inventory.Adjust(pid, dir, Qty, Note ?? "");
        TempData["Flash"] = result.Succeeded ? "Stock adjusted." : result.ErrorMessage;
        return RedirectToPage(new { productId = pid });
    }

    private void Load()
    {
        // Load only active products, limited to a reasonable number for the dropdown (100 should be more than enough)
        var ps = products.Search(new QueryOptions { PageSize = 100, SortBy = "name" }, activeOnly: true);
        if (ps.Succeeded)
            ProductOptions = ps.Value.Items.Select(p => new SelectListItem($"{p.Sku} — {p.Name}", p.Id.ToString())).ToList();

        if (ProductId is int pid)
        {
            var one = products.GetById(pid);
            if (one.Succeeded) Selected = one.Value;
            var led = inventory.GetLedger(pid, new QueryOptions { PageSize = 100 });
            if (led.Succeeded) Ledger = led.Value;
        }
    }
}
