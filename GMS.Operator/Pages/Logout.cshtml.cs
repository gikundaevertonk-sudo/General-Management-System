using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GMS.Operator.Pages;

// Kept non-anonymous. Inside GMS.Web this was load-bearing: the operator cookie was a
// non-default scheme, so an [AllowAnonymous] sign-out page saw no identity, the antiforgery
// token had been minted under the operator's, and the mismatch returned 400 - the Sign out
// button did nothing for four hours. Here the cookie is the default scheme, so that trap is
// gone and anonymous would work. Requiring sign-in anyway says what this page is for, and
// signing out when not signed in has nothing to do but redirect, which is what OnGet does.
[Authorize]
public class LogoutModel : PageModel
{
    public async Task<IActionResult> OnPostAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage("/Login");
    }

    public IActionResult OnGet() => RedirectToPage("/Login");
}
