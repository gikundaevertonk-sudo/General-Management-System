using GMS.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GMS.Web.Pages.Categories;

[Authorize("perm:categories.edit")]
public class EditModel(CategoryService categories) : PageModel
{
    [BindProperty(SupportsGet = true)] public int? Id { get; set; }
    [BindProperty] public string Name { get; set; } = "";
    [BindProperty] public string? Description { get; set; }
    [BindProperty] public int? ParentCategoryId { get; set; }
    public string? Error { get; private set; }
    public List<SelectListItem> ParentOptions { get; private set; } = [];
    public bool IsNew => Id is null;

    public IActionResult OnGet()
    {
        LoadParents();
        if (Id is int id)
        {
            var result = categories.GetById(id);
            if (result.Failed) return NotFound();
            Name = result.Value.Name;
            Description = result.Value.Description;
            ParentCategoryId = result.Value.ParentCategoryId;
        }
        return Page();
    }

    public IActionResult OnPost()
    {
        LoadParents();
        var result = Id is int id
            ? categories.Update(id, Name, Description ?? "", ParentCategoryId)
            : (GMS.Core.Common.Result)categories.Create(Name, Description ?? "", ParentCategoryId);
        if (result.Failed) { Error = result.ErrorMessage; return Page(); }

        TempData["Flash"] = IsNew ? "Category created." : "Category saved.";
        return RedirectToPage("Index");
    }

    private void LoadParents()
    {
        var result = categories.List();
        ParentOptions = [new SelectListItem("— none —", "")];
        if (result.Succeeded)
        {
            ParentOptions.AddRange(result.Value
                .Where(c => c.Id != Id) // a category can't be its own parent
                .Select(c => new SelectListItem(c.Name, c.Id.ToString())));
        }
    }
}
