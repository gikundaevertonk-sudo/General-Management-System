using GMS.Core.Common;
using GMS.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GMS.Web.Pages.Shops;

[Authorize("perm:shops.manage")]
public class EditModel(ShopService shops) : PageModel
{
    [BindProperty(SupportsGet = true)] public int? Id { get; set; }
    [BindProperty] public ShopInput Input { get; set; } = new();
    public string? Error { get; private set; }
    public bool IsNew => Id is null;

    public IActionResult OnGet()
    {
        if (Id is int id)
        {
            var result = shops.GetById(id);
            if (result.Failed) return NotFound();
            var s = result.Value;
            Input = new ShopInput
            {
                Name = s.Name, Code = s.Code, Address = s.Address,
                Phone = s.Phone, IsActive = s.IsActive,
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
        Result result = Id is int id ? shops.Update(id, Input) : shops.Create(Input);
        if (result.Failed) { Error = result.ErrorMessage; return Page(); }

        TempData["Flash"] = IsNew ? "Shop created." : "Shop saved.";
        return RedirectToPage("Index");
    }
}
