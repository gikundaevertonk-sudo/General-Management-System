using GMS.Core.Models;
using GMS.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GMS.Web.Pages.Categories;

[Authorize("perm:categories.view")]
public class IndexModel(CategoryService categories) : PageModel
{
    public IReadOnlyList<Category> Categories { get; private set; } = [];
    public IReadOnlyDictionary<int, string> ParentNames { get; private set; } = new Dictionary<int, string>();
    public string? Error { get; private set; }

    public void OnGet() => Load();

    public IActionResult OnPostDelete(int id)
    {
        var r = categories.Delete(id);
        TempData["Flash"] = r.Succeeded ? "Category deleted." : r.ErrorMessage;
        return RedirectToPage();
    }

    private void Load()
    {
        var result = categories.List();
        if (result.Failed) { Error = result.ErrorMessage; return; }
        Categories = result.Value;
        ParentNames = Categories.ToDictionary(c => c.Id, c => c.Name);
    }
}
