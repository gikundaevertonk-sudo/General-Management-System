using GMS.Core.Enums;
using GMS.Core.Models;
using GMS.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GMS.Web.Pages.Transactions;

[Authorize("perm:transactions.view")]
public class EditModel(
    TransactionService transactions,
    ProductService products,
    CustomerService customers,
    SupplierService suppliers) : PageModel
{
    [BindProperty(SupportsGet = true)] public int? Id { get; set; }
    [BindProperty(SupportsGet = true)] public TransactionType? Type { get; set; }

    public Transaction? Txn { get; private set; }
    public string? Error { get; private set; }

    public List<SelectListItem> PartyOptions { get; private set; } = new();
    public List<SelectListItem> ProductOptions { get; private set; } = new();

    // new-draft form
    [BindProperty] public int PartyId { get; set; }
    [BindProperty] public DateTime TxnDate { get; set; } = DateTime.Today;
    [BindProperty] public string? Notes { get; set; }

    // add-line form
    [BindProperty] public int LineProductId { get; set; }
    [BindProperty] public decimal LineQty { get; set; } = 1;
    [BindProperty] public decimal? LineUnitPrice { get; set; }
    [BindProperty] public decimal? LineTaxPercent { get; set; }

    public bool IsDraft => Txn?.Status == TransactionStatus.Draft;
    public bool NeedsParty => (Txn?.Type ?? Type) is TransactionType.Sale or TransactionType.Purchase;

    public IActionResult OnGet()
    {
        if (Id is null && Type is null) return RedirectToPage("Index");
        LoadLookups(Type);
        if (Id is int id)
        {
            var result = transactions.GetById(id);
            if (result.Failed) return NotFound();
            Txn = result.Value;
            LoadLookups(Txn.Type);
        }
        return Page();
    }

    public IActionResult OnPostCreate()
    {
        var type = Type ?? TransactionType.Sale;
        int? party = type is TransactionType.Sale or TransactionType.Purchase ? PartyId : null;
        var result = transactions.CreateDraft(type, party, TxnDate, Notes ?? "");
        if (result.Failed) { Error = result.ErrorMessage; LoadLookups(type); return Page(); }
        return RedirectToPage("Edit", new { id = result.Value.Id });
    }

    public IActionResult OnPostAddLine()
    {
        if (Id is not int id) return RedirectToPage("Index");
        var input = new TransactionLineInput
        {
            ProductId = LineProductId,
            Quantity = LineQty,
            UnitPrice = LineUnitPrice,
            TaxRatePercent = LineTaxPercent,
        };
        var result = transactions.AddLine(id, input);
        if (result.Failed) TempData["Flash"] = result.ErrorMessage;
        return RedirectToPage("Edit", new { id });
    }

    public IActionResult OnPostRemoveLine(int lineId)
    {
        if (Id is not int id) return RedirectToPage("Index");
        var result = transactions.RemoveLine(id, lineId);
        if (result.Failed) TempData["Flash"] = result.ErrorMessage;
        return RedirectToPage("Edit", new { id });
    }

    public IActionResult OnPostConfirm()
    {
        if (Id is not int id) return RedirectToPage("Index");
        var result = transactions.Confirm(id);
        TempData["Flash"] = result.Succeeded ? "Transaction confirmed." : result.ErrorMessage;
        return RedirectToPage("Edit", new { id });
    }

    public IActionResult OnPostCancel()
    {
        if (Id is not int id) return RedirectToPage("Index");
        var result = transactions.Cancel(id, "Cancelled from web");
        TempData["Flash"] = result.Succeeded ? "Transaction cancelled." : result.ErrorMessage;
        return RedirectToPage("Edit", new { id });
    }

    private void LoadLookups(TransactionType? type)
    {
        var ps = products.Search(new GMS.Core.Common.QueryOptions { PageSize = 1000, SortBy = "name" }, activeOnly: true);
        ProductOptions = ps.Succeeded
            ? ps.Value.Items.Select(p => new SelectListItem($"{p.Sku} — {p.Name}", p.Id.ToString())).ToList()
            : new();

        PartyOptions = new();
        if (type == TransactionType.Sale)
        {
            var cs = customers.Search(new GMS.Core.Common.QueryOptions { PageSize = 1000 });
            if (cs.Succeeded) PartyOptions = cs.Value.Items.Select(c => new SelectListItem(c.Name, c.Id.ToString())).ToList();
        }
        else if (type == TransactionType.Purchase)
        {
            var ss = suppliers.Search(new GMS.Core.Common.QueryOptions { PageSize = 1000 });
            if (ss.Succeeded) PartyOptions = ss.Value.Items.Select(s => new SelectListItem(s.Name, s.Id.ToString())).ToList();
        }
    }
}
