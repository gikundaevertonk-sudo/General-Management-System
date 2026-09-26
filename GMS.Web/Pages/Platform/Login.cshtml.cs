using System.Security.Claims;
using GMS.Web.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace GMS.Web.Pages.Platform;

[AllowAnonymous]
public class LoginModel(IOptions<PlatformOperatorOptions> options) : PageModel
{
    private readonly PlatformOperatorOptions _operator = options.Value;

    [BindProperty] public string UserName { get; set; } = string.Empty;
    [BindProperty] public string Password { get; set; } = string.Empty;
    public string? Error { get; private set; }

    public IActionResult OnGet()
    {
        // No credentials configured means this deployment has no operator console at all.
        // 404 rather than a sign-in form: nothing here to probe, and nothing to guess at.
        if (!_operator.IsConfigured) return NotFound();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!_operator.IsConfigured) return NotFound();

        if (!_operator.Verify(UserName, Password))
        {
            Error = "Invalid credentials.";
            return Page();
        }

        // The identity carries no organization claim and no permission claims - it is not a
        // tenant user and must never be treated as one. WebCurrentUser.IsPlatformOperator keys
        // off the scheme name below, not off anything in here.
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, _operator.UserName)],
            PlatformAuth.Scheme);

        await HttpContext.SignInAsync(
            PlatformAuth.Scheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = false });

        return RedirectToPage("/Platform/Index");
    }
}
