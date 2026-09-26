using GMS.Web.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GMS.Web.Pages.Platform;

[AllowAnonymous]
public class LogoutModel : PageModel
{
    public async Task<IActionResult> OnPostAsync()
    {
        // Only the operator cookie: a tenant session in the same browser is left alone.
        await HttpContext.SignOutAsync(PlatformAuth.Scheme);
        return RedirectToPage("/Platform/Login");
    }

    public IActionResult OnGet() => RedirectToPage("/Platform/Login");
}
