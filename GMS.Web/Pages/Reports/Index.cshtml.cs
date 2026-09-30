using System.Collections;
using System.Text;
using GMS.Core.Abstractions;
using GMS.Core.Contracts;
using GMS.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GMS.Web.Pages.Reports;

[Authorize("perm:reports.view")]
public class IndexModel(ReportService reports, ShopService shops, ICurrentUser me) : PageModel
{
    [BindProperty(SupportsGet = true)] public string Report { get; set; } = "sales";
    [BindProperty(SupportsGet = true)] public DateTime? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? To { get; set; }

    /// <summary>
    /// Which shop to report on; "" is the whole organisation. A string because "" is a real
    /// choice and an int? bound from an empty option cannot be told apart from a missing one.
    /// </summary>
    [BindProperty(SupportsGet = true)] public string? Shop { get; set; }

    public SalesSummaryReport? Sales { get; private set; }
    public IReadOnlyList<InventoryValuationRow>? Valuation { get; private set; }
    public IReadOnlyList<InventoryValuationRow>? LowStock { get; private set; }
    public string? Error { get; private set; }

    public List<SelectListItem> ShopOptions { get; private set; } = new();

    /// <summary>
    /// A shop attendant gets their own branch's figures and no picker: the service pins them
    /// there regardless, so offering a choice would only be a lie.
    /// </summary>
    public bool CanChooseShop => me.ShopId is null && ShopOptions.Count > 1;

    private int? ShopId => string.IsNullOrEmpty(Shop) ? null : int.Parse(Shop);

    private DateRange Range =>
        From is { } f && To is { } t
            ? new DateRange(DateTime.SpecifyKind(f.Date, DateTimeKind.Utc), DateTime.SpecifyKind(t.Date.AddDays(1), DateTimeKind.Utc))
            : DateRange.MonthToDate(DateTime.UtcNow);

    public void OnGet()
    {
        LoadShops();

        switch (Report)
        {
            case "valuation":
                var v = reports.InventoryValuation(ShopId);
                if (v.Succeeded) Valuation = v.Value; else Error = v.ErrorMessage;
                break;
            case "lowstock":
                var l = reports.LowStock(ShopId);
                if (l.Succeeded) LowStock = l.Value; else Error = l.ErrorMessage;
                break;
            default:
                Report = "sales";
                var s = reports.SalesSummary(Range, ShopId);
                if (s.Succeeded) Sales = s.Value; else Error = s.ErrorMessage;
                break;
        }
    }

    public IActionResult OnGetCsv()
    {
        IEnumerable rows = Report switch
        {
            "valuation" => reports.InventoryValuation(ShopId).Value,
            "lowstock" => reports.LowStock(ShopId).Value
                .Select(r => new { r.Sku, r.ProductName, r.QuantityOnHand, r.BelowReorderLevel }).ToList(),
            _ => reports.SalesSummary(Range, ShopId).Value.Lines,
        };
        var csv = ReportService.ToCsv(rows);
        return File(Encoding.UTF8.GetBytes(csv), "text/csv", $"{Report}-report.csv");
    }

    private void LoadShops()
    {
        ShopOptions = new() { new SelectListItem("Whole organisation", "") };
        var list = shops.List();
        if (list.Succeeded)
            ShopOptions.AddRange(list.Value.Select(s => new SelectListItem(s.Name, s.Id.ToString())));
    }
}
