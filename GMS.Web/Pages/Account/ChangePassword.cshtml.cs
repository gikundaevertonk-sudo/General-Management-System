using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using GMS.Core.Services;
using GMS.Web.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GMS.Web.Pages.Account;

[AllowAnonymous] // folder convention; the user is still authenticated via the cookie
public class ChangePasswordModel(AuthService auth) : PageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    public string? Error { get; private set; }
    public bool Forced { get; private set; }

    public sealed class InputModel
    {
        [Required, DataType(DataType.Password)] public string CurrentPassword { get; set; } = "";
        [Required, DataType(DataType.Password), MinLength(8)] public string NewPassword { get; set; } = "";
        [Required, DataType(DataType.Password), Compare(nameof(NewPassword))] public string ConfirmPassword { get; set; } = "";
    }

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated != true) return RedirectToPage("/Account/Login");
        Forced = User.HasClaim(Claims.MustChangePassword, "1");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (User.Identity?.IsAuthenticated != true) return RedirectToPage("/Account/Login");
        Forced = User.HasClaim(Claims.MustChangePassword, "1");

        if (!ModelState.IsValid) return Page();

        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = auth.ChangePassword(userId, Input.CurrentPassword, Input.NewPassword);
        if (result.Failed)
        {
            Error = result.ErrorMessage;
            return Page();
        }

        // Refresh the cookie so the must-change flag clears.
        var principal = auth.GetPrincipal(userId);
        if (principal.Succeeded)
        {
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                Claims.BuildPrincipal(principal.Value, CookieAuthenticationDefaults.AuthenticationScheme));
        }

        TempData["Flash"] = "Password updated.";
        return RedirectToPage("/Index");
    }
}
