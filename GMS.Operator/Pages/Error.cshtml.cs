using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GMS.Operator.Pages;

/// <summary>
/// Anonymous on purpose: an error page that needs a valid session cannot report the error that
/// broke the session.
/// </summary>
[AllowAnonymous]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class ErrorModel : PageModel
{
    public string? RequestId { get; private set; }

    public void OnGet() =>
        RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
}
