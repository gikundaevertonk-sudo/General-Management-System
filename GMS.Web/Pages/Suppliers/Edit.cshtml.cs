using GMS.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GMS.Web.Pages.Suppliers;

[Authorize("perm:suppliers.edit")]
public class EditModel(SupplierService suppliers) : PageModel
{
    [BindProperty(SupportsGet = true)] public int? Id { get; set; }
    [BindProperty] public SupplierInput Input { get; set; } = new();
    public string? Error { get; private set; }
    public bool IsNew => Id is null;

    public IActionResult OnGet()
    {
        if (Id is int id)
        {
            var result = suppliers.GetById(id);
            if (result.Failed) return NotFound();
            var s = result.Value;
            Input = new SupplierInput
            {
                Name = s.Name, ContactName = s.ContactName, Email = s.Email,
                Phone = s.Phone, Address = s.Address, IsActive = s.IsActive,
            };
        }
        else
        {
            Input.IsActive = true;
        }
        return Page();
    }

    public IActionResult OnPost()
    {
        var result = Id is int id ? suppliers.Update(id, Input) : (GMS.Core.Common.Result)suppliers.Create(Input);
        if (result.Failed) { Error = result.ErrorMessage; return Page(); }

        TempData["Flash"] = IsNew ? "Supplier created." : "Supplier saved.";
        return RedirectToPage("Index");
    }
}
