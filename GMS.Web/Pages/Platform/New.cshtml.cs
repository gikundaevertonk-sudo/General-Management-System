using GMS.Core.Services;
using GMS.Web.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GMS.Web.Pages.Platform;

[Authorize(AuthenticationSchemes = PlatformAuth.Scheme, Policy = PlatformAuth.Policy)]
public class NewModel(OrganizationService organizations) : PageModel
{
    [BindProperty] public string Name { get; set; } = string.Empty;
    [BindProperty] public string Code { get; set; } = string.Empty;
    [BindProperty] public string Email { get; set; } = string.Empty;

    public string? Error { get; private set; }

    /// <summary>Set after a successful create, so the handover details can be shown once.</summary>
    public string? CreatedCode { get; private set; }
    public string? CreatedName { get; private set; }

    /// <summary>What DataSeeder gives every new organisation's first administrator.</summary>
    public static string SeededAdminUserName => DataSeeder.DefaultAdminUserName;
    public static string SeededAdminPassword => DataSeeder.DefaultAdminPassword;

    public void OnGet() { }

    public IActionResult OnPost()
    {
        var result = organizations.CreateTrial(Name.Trim(), Code.Trim(), Email.Trim());
        if (result.Failed)
        {
            Error = result.ErrorMessage;
            return Page();
        }

        CreatedName = result.Value.Name;
        CreatedCode = result.Value.Code;
        return Page();
    }
}
