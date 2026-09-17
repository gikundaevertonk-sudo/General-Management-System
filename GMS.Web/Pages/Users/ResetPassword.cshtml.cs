using System.ComponentModel.DataAnnotations;
using GMS.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GMS.Web.Pages.Users;

[Authorize("perm:users.manage")]
public class ResetPasswordModel(UserService users) : PageModel
{
    [BindProperty(SupportsGet = true)] public int Id { get; set; }
    [BindProperty, Required, MinLength(8)] public string NewPassword { get; set; } = "";
    public string? UserName { get; private set; }
    public string? Error { get; private set; }

    public IActionResult OnGet()
    {
        var u = users.GetById(Id);
        if (u.Failed) return NotFound();
        UserName = u.Value.UserName;
        return Page();
    }

    public IActionResult OnPost()
    {
        if (!ModelState.IsValid) { var u = users.GetById(Id); UserName = u.Succeeded ? u.Value.UserName : null; return Page(); }

        var result = users.ResetPassword(Id, NewPassword);
        if (result.Failed) { Error = result.ErrorMessage; return Page(); }

        TempData["Flash"] = "Password reset. The user must change it at next sign-in.";
        return RedirectToPage("Index");
    }
}
