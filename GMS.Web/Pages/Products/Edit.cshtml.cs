using GMS.Core.Common;
using GMS.Core.Models;
using GMS.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GMS.Web.Pages.Products;

[Authorize("perm:products.edit")]
public class EditModel(ProductService products, CategoryService categories) : PageModel
{
    [BindProperty(SupportsGet = true)] public int? Id { get; set; }
    [BindProperty] public ProductInput Input { get; set; } = new();
    public string? Error { get; private set; }
    public List<SelectListItem> Categories { get; private set; } = new();
    public bool IsNew => Id is null;

    public IActionResult OnGet()
    {
        LoadCategories();
        if (Id is int id)
        {
            var result = products.GetById(id);
            if (result.Failed) return NotFound();
            var p = result.Value;
            Input = new ProductInput
            {
                Sku = p.Sku, Name = p.Name, Description = p.Description, CategoryId = p.CategoryId,
                UnitPrice = p.UnitPrice, CostPrice = p.CostPrice, UnitOfMeasure = p.UnitOfMeasure,
                ReorderLevel = p.ReorderLevel, IsActive = p.IsActive,
            };
        }
        else
        {
            Input.IsActive = true;
            Input.UnitOfMeasure = "each";
        }
        return Page();
    }

    public IActionResult OnPost()
    {
        LoadCategories();
        var result = Id is int id ? products.Update(id, Input) : (Result)products.Create(Input);
        if (result.Failed) { Error = result.ErrorMessage; return Page(); }

        TempData["Flash"] = Id is null ? "Product created." : "Product saved.";
        return RedirectToPage("Index");
    }

    private void LoadCategories()
    {
        Categories = new() { new SelectListItem("— none —", "") };
        var cats = categories.List();
        if (cats.Succeeded)
            Categories.AddRange(cats.Value.Select(c => new SelectListItem(c.Name, c.Id.ToString())));
    }
}
