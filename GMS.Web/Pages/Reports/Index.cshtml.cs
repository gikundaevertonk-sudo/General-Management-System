using System.Collections;
using System.Text;
using GMS.Core.Contracts;
using GMS.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GMS.Web.Pages.Reports;

[Authorize("perm:reports.view")]
public class IndexModel(ReportService reports) : PageModel
{
    [BindProperty(SupportsGet = true)] public string Report { get; set; } = "sales";
    [BindProperty(SupportsGet = true)] public DateTime? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? To { get; set; }

    public SalesSummaryReport? Sales { get; private set; }
    public IReadOnlyList<InventoryValuationRow>? Valuation { get; private set; }
    public IReadOnlyList<GMS.Core.Models.Product>? LowStock { get; private set; }
    public string? Error { get; private set; }

    private DateRange Range =>
        From is { } f && To is { } t
            ? new DateRange(DateTime.SpecifyKind(f.Date, DateTimeKind.Utc), DateTime.SpecifyKind(t.Date.AddDays(1), DateTimeKind.Utc))
            : DateRange.MonthToDate(DateTime.UtcNow);

    public void OnGet()
    {
        switch (Report)
        {
            case "valuation":
                var v = reports.InventoryValuation();
                if (v.Succeeded) Valuation = v.Value; else Error = v.ErrorMessage;
                break;
            case "lowstock":
                var l = reports.LowStock();
                if (l.Succeeded) LowStock = l.Value; else Error = l.ErrorMessage;
                break;
            default:
                Report = "sales";
                var s = reports.SalesSummary(Range);
                if (s.Succeeded) Sales = s.Value; else Error = s.ErrorMessage;
                break;
        }
    }

    public IActionResult OnGetCsv()
    {
        IEnumerable rows = Report switch
        {
            "valuation" => reports.InventoryValuation().Value,
            "lowstock" => reports.LowStock().Value
                .Select(p => new { p.Sku, p.Name, p.QuantityOnHand, p.ReorderLevel }).ToList(),
            _ => reports.SalesSummary(Range).Value.Lines,
        };
        var csv = ReportService.ToCsv(rows);
        return File(Encoding.UTF8.GetBytes(csv), "text/csv", $"{Report}-report.csv");
    }
}
