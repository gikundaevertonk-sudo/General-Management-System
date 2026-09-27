using System.Security.Claims;
using GMS.Operator.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace GMS.Operator.Pages;

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

        // A name and nothing else. No organization claim and no permission claims, because the
        // operator is not a tenant user and holds no authority inside anyone's organization -
        // OperatorCurrentUser answers false to both and the services rely on that.
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, _operator.UserName)],
            CookieAuthenticationDefaults.AuthenticationScheme);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = false });

        return RedirectToPage("/Index");
    }
}
