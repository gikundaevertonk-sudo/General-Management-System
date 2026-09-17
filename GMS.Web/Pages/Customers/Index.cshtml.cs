using GMS.Core.Common;
using GMS.Core.Models;
using GMS.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GMS.Web.Pages.Customers;

[Authorize("perm:customers.view")]
public class IndexModel(CustomerService customers) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public int PageNo { get; set; } = 1;

    public PagedResult<Customer>? Results { get; private set; }

    public void OnGet()
    {
        var opts = new QueryOptions { Search = Q ?? "", Page = PageNo, PageSize = 20 };
        var result = customers.Search(opts);
        if (result.Succeeded) Results = result.Value;
    }

    public IActionResult OnPostToggle(int id, bool active)
    {
        var r = customers.SetActive(id, !active);
        TempData["Flash"] = r.Succeeded ? "Customer updated." : r.ErrorMessage;
        return RedirectToPage(new { Q, PageNo });
    }
}
