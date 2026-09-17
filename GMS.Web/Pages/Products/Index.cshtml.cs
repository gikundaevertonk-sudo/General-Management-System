using GMS.Core.Common;
using GMS.Core.Models;
using GMS.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GMS.Web.Pages.Products;

[Authorize("perm:products.view")]
public class IndexModel(ProductService products, CategoryService categories) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public int PageNo { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public bool Low { get; set; }

    public PagedResult<Product>? Results { get; private set; }
    public IReadOnlyDictionary<int, string> CategoryNames { get; private set; } =
        new Dictionary<int, string>();

    public void OnGet()
    {
        var opts = new QueryOptions { Search = Q ?? "", Page = PageNo, PageSize = 20, SortBy = "name" };
        var result = products.Search(opts, lowStockOnly: Low);
        if (result.Succeeded) Results = result.Value;

        var cats = categories.List();
        if (cats.Succeeded)
            CategoryNames = cats.Value.ToDictionary(c => c.Id, c => c.Name);
    }
}
