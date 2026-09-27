using GMS.Web.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GMS.Web.Pages.Platform;

// Not [AllowAnonymous], however much a sign-out looks like it needs nothing. The operator
// cookie is a non-default scheme, so UseAuthentication never populates HttpContext.User with
// it - only an [Authorize] naming the scheme does. On an anonymous page the POST therefore
// arrived with no identity, while the token in the navbar form had been minted under the
// operator's, and antiforgery rejected the mismatch with a 400: the console's Sign out button
// did nothing at all, leaving the cookie to run out its four hours. The tenant's own logout is
// anonymous and works, because its cookie IS the default scheme and is always authenticated.
// Requiring the scheme here establishes the identity, so the token matches. Signing out when
// not signed in simply redirects to the login page, which is what OnGet does anyway.
[Authorize(AuthenticationSchemes = PlatformAuth.Scheme, Policy = PlatformAuth.Policy)]
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
