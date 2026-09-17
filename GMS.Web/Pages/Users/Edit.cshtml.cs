using GMS.Core.Common;
using GMS.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GMS.Web.Pages.Users;

[Authorize("perm:users.manage")]
public class EditModel(UserService users, RoleService roles) : PageModel
{
    [BindProperty(SupportsGet = true)] public int? Id { get; set; }
    [BindProperty] public UserInput Input { get; set; } = new();
    [BindProperty] public string? TempPassword { get; set; }
    public string? Error { get; private set; }
    public List<SelectListItem> RoleOptions { get; private set; } = new();
    public bool IsNew => Id is null;

    public IActionResult OnGet()
    {
        LoadRoles();
        if (Id is int id)
        {
            var result = users.GetById(id);
            if (result.Failed) return NotFound();
            var u = result.Value;
            Input = new UserInput { UserName = u.UserName, Email = u.Email, FullName = u.FullName, RoleId = u.RoleId, IsActive = u.IsActive };
        }
        else
        {
            Input.IsActive = true;
        }
        return Page();
    }

    public IActionResult OnPost()
    {
        LoadRoles();
        Result result = Id is int id
            ? users.Update(id, Input)
            : users.Create(Input, TempPassword ?? "");
        if (result.Failed) { Error = result.ErrorMessage; return Page(); }

        TempData["Flash"] = IsNew ? "User created." : "User saved.";
        return RedirectToPage("Index");
    }

    private void LoadRoles()
    {
        var rl = roles.List();
        RoleOptions = rl.Succeeded
            ? rl.Value.Select(r => new SelectListItem(r.Role.Name, r.Role.Id.ToString())).ToList()
            : new();
    }
}
