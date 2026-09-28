using GMS.Core.Common;
using GMS.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GMS.Web.Pages.Users;

[Authorize("perm:users.manage")]
public class EditModel(UserService users, RoleService roles, ShopService shops) : PageModel
{
    [BindProperty(SupportsGet = true)] public int? Id { get; set; }
    [BindProperty] public UserInput Input { get; set; } = new();
    [BindProperty] public string? TempPassword { get; set; }

    /// <summary>
    /// Posted as a string because "" is a real choice here - it means the whole organisation - and
    /// an int? bound from an empty option cannot be told apart from one that was not submitted.
    /// </summary>
    [BindProperty] public string? ShopChoice { get; set; }

    public string? Error { get; private set; }
    public List<SelectListItem> RoleOptions { get; private set; } = new();
    public List<SelectListItem> ShopOptions { get; private set; } = new();
    public bool IsNew => Id is null;
    public bool HasShops => ShopOptions.Count > 1;

    public IActionResult OnGet()
    {
        LoadLookups();
        if (Id is int id)
        {
            var result = users.GetById(id);
            if (result.Failed) return NotFound();
            var u = result.Value;
            Input = new UserInput
            {
                UserName = u.UserName, Email = u.Email, FullName = u.FullName,
                RoleId = u.RoleId, ShopId = u.ShopId, IsActive = u.IsActive,
            };
            ShopChoice = u.ShopId?.ToString() ?? "";
        }
        else
        {
            Input.IsActive = true;
        }
        return Page();
    }

    public IActionResult OnPost()
    {
        LoadLookups();
        Input.ShopId = string.IsNullOrEmpty(ShopChoice) ? null : int.Parse(ShopChoice);

        Result result = Id is int id
            ? users.Update(id, Input)
            : users.Create(Input, TempPassword ?? "");
        if (result.Failed) { Error = result.ErrorMessage; return Page(); }

        TempData["Flash"] = IsNew ? "User created." : "User saved.";
        return RedirectToPage("Index");
    }

    private void LoadLookups()
    {
        var rl = roles.List();
        RoleOptions = rl.Succeeded
            ? rl.Value.Select(r => new SelectListItem(r.Role.Name, r.Role.Id.ToString())).ToList()
            : new();

        ShopOptions = new() { new SelectListItem("All shops (no restriction)", "") };
        var sl = shops.List();
        if (sl.Succeeded)
            ShopOptions.AddRange(sl.Value.Select(s => new SelectListItem(s.Name, s.Id.ToString())));
    }
}
