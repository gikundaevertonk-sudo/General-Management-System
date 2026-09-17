using GMS.Core.Common;
using GMS.Core.Models;
using GMS.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GMS.Web.Pages.Users;

[Authorize("perm:users.view")]
public class IndexModel(UserService users, RoleService roles) : PageModel
{
    [BindProperty(SupportsGet = true)] public string Tab { get; set; } = "users";

    public PagedResult<User>? Users { get; private set; }
    public IReadOnlyList<RoleDetail>? Roles { get; private set; }
    public IReadOnlyDictionary<int, string> RoleNames { get; private set; } = new Dictionary<int, string>();

    public void OnGet()
    {
        var rl = roles.List();
        if (rl.Succeeded)
        {
            Roles = rl.Value;
            RoleNames = rl.Value.ToDictionary(r => r.Role.Id, r => r.Role.Name);
        }

        if (Tab != "roles")
        {
            var u = users.Search(new QueryOptions { PageSize = 200 });
            if (u.Succeeded) Users = u.Value;
        }
    }

    public IActionResult OnPostToggle(int id, bool active)
    {
        var r = users.SetActive(id, !active);
        TempData["Flash"] = r.Succeeded ? "User updated." : r.ErrorMessage;
        return RedirectToPage();
    }
}
