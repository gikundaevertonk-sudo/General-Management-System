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

        var cs = customers.Search(new QueryOptions { PageSize = 1000 });
        if (cs.Succeeded) CustomerNames = cs.Value.Items.ToDictionary(c => c.Id, c => c.Name);
        var ss = suppliers.Search(new QueryOptions { PageSize = 1000 });
        if (ss.Succeeded) SupplierNames = ss.Value.Items.ToDictionary(s => s.Id, s => s.Name);
    }

    public string PartyOf(Transaction t) =>
        t.CustomerId is int c ? CustomerNames.GetValueOrDefault(c, "—")
        : t.SupplierId is int s ? SupplierNames.GetValueOrDefault(s, "—")
        : "—";
}
