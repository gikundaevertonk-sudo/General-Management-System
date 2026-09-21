using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GMS.Web.Pages.Account;

[AllowAnonymous]
public class SuspendedModel : PageModel
{
    public void OnGet()
    {
    }
}
