using GMS.Core.Common;
using GMS.Core.Enums;
using GMS.Core.Models;
using GMS.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GMS.Web.Pages.Transactions;

[Authorize("perm:transactions.view")]
public class IndexModel(TransactionService transactions, CustomerService customers, SupplierService suppliers) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public TransactionType? Type { get; set; }
    [BindProperty(SupportsGet = true)] public TransactionStatus? Status { get; set; }
    [BindProperty(SupportsGet = true)] public int PageNo { get; set; } = 1;

    public PagedResult<Transaction>? Results { get; private set; }
    public IReadOnlyDictionary<int, string> CustomerNames { get; private set; } = new Dictionary<int, string>();
    public IReadOnlyDictionary<int, string> SupplierNames { get; private set; } = new Dictionary<int, string>();

    public void OnGet() => Load();

    public IActionResult OnPostConfirm(int id)
    {
        var r = transactions.Confirm(id);
        TempData["Flash"] = r.Succeeded ? "Transaction confirmed." : r.ErrorMessage;
        return RedirectToPage(new { Q, Type, Status, PageNo });
    }

    public IActionResult OnPostCancel(int id)
    {
        var r = transactions.Cancel(id, "Cancelled from web");
        TempData["Flash"] = r.Succeeded ? "Transaction cancelled." : r.ErrorMessage;
        return RedirectToPage(new { Q, Type, Status, PageNo });
    }

    private void Load()
    {
        var opts = new QueryOptions { Search = Q ?? "", Page = PageNo, PageSize = 20 };
        var result = transactions.Search(opts, Type, Status);
        if (result.Succeeded) Results = result.Value;

        // Load names only for customers/suppliers referenced in the displayed transactions
        var customerIds = Results?.Items.Where(t => t.CustomerId.HasValue).Select(t => t.CustomerId!.Value).Distinct().ToList() ?? [];
        var supplierIds = Results?.Items.Where(t => t.SupplierId.HasValue).Select(t => t.SupplierId!.Value).Distinct().ToList() ?? [];

        CustomerNames = new Dictionary<int, string>();
        SupplierNames = new Dictionary<int, string>();

        if (customerIds.Any())
        {
            var cs = customers.Search(new QueryOptions { PageSize = customerIds.Count });
            if (cs.Succeeded)
            {
                CustomerNames = cs.Value.Items.Where(c => customerIds.Contains(c.Id)).ToDictionary(c => c.Id, c => c.Name);
            }
        }

        if (supplierIds.Any())
        {
            var ss = suppliers.Search(new QueryOptions { PageSize = supplierIds.Count });
            if (ss.Succeeded)
            {
                SupplierNames = ss.Value.Items.Where(s => supplierIds.Contains(s.Id)).ToDictionary(s => s.Id, s => s.Name);
            }
        }
    }

    public string PartyOf(Transaction t) =>
        t.CustomerId is int c ? CustomerNames.GetValueOrDefault(c, "—")
        : t.SupplierId is int s ? SupplierNames.GetValueOrDefault(s, "—")
        : IsWalkIn(t) ? "One-off sale"
        : "—";

    /// <summary>A sale with nobody attached: a counter sale, recorded without an account.</summary>
    public static bool IsWalkIn(Transaction t) =>
        t.Type == TransactionType.Sale && t.CustomerId is null;
}
