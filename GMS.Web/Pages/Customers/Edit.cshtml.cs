using GMS.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GMS.Web.Pages.Customers;

[Authorize("perm:customers.edit")]
public class EditModel(CustomerService customers) : PageModel
{
    [BindProperty(SupportsGet = true)] public int? Id { get; set; }
    [BindProperty] public CustomerInput Input { get; set; } = new();
    public string? Error { get; private set; }
    public bool IsNew => Id is null;

    public IActionResult OnGet()
    {
        if (Id is int id)
        {
            var result = customers.GetById(id);
            if (result.Failed) return NotFound();
            var c = result.Value;
            Input = new CustomerInput
            {
                Code = c.Code, Name = c.Name, ContactName = c.ContactName, Email = c.Email,
                Phone = c.Phone, BillingAddress = c.BillingAddress, Notes = c.Notes, IsActive = c.IsActive,
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
        var result = Id is int id ? customers.Update(id, Input) : (GMS.Core.Common.Result)customers.Create(Input);
        if (result.Failed) { Error = result.ErrorMessage; return Page(); }

        TempData["Flash"] = IsNew ? "Customer created." : "Customer saved.";
        return RedirectToPage("Index");
    }
}
