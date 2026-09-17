using System.ComponentModel.DataAnnotations;
using GMS.Core.Services;
using GMS.Web.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GMS.Web.Pages.Account;

[AllowAnonymous]
public class LoginModel(AuthService auth) : PageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }
    public string? Error { get; private set; }

    public sealed class InputModel
    {
        [Required] public string UserName { get; set; } = "";
        [Required, DataType(DataType.Password)] public string Password { get; set; } = "";
    }

    public void OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
            Response.Redirect("/");
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var result = auth.SignIn(Input.UserName, Input.Password);
        if (result.Failed)
        {
            Error = result.ErrorMessage;
            return Page();
        }

        var principal = Claims.BuildPrincipal(result.Value, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        if (result.Value.MustChangePassword)
            return RedirectToPage("/Account/ChangePassword");

        return LocalRedirect(string.IsNullOrEmpty(ReturnUrl) ? "/" : ReturnUrl);
    }
}
