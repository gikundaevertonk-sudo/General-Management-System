using GMS.Core.Common;
using GMS.Core.Models;
using GMS.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GMS.Web.Pages.Suppliers;

[Authorize("perm:suppliers.view")]
public class IndexModel(SupplierService suppliers) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public int PageNo { get; set; } = 1;

    public PagedResult<Supplier>? Results { get; private set; }

    public void OnGet()
    {
        var opts = new QueryOptions { Search = Q ?? "", Page = PageNo, PageSize = 20 };
        var result = suppliers.Search(opts);
        if (result.Succeeded) Results = result.Value;
    }

    public IActionResult OnPostToggle(int id, bool active)
    {
        var r = suppliers.SetActive(id, !active);
        TempData["Flash"] = r.Succeeded ? "Supplier updated." : r.ErrorMessage;
        return RedirectToPage(new { Q, PageNo });
    }
}
