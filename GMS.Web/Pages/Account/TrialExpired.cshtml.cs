using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GMS.Web.Pages.Account;

[AllowAnonymous]
public class TrialExpiredModel : PageModel
{
    public void OnGet()
    {
    }
}
