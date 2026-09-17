using GMS.Core.Common;
using GMS.Core.Models;
using GMS.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GMS.Web.Pages.Audit;

[Authorize("perm:audit.view")]
public class IndexModel(AuditService audit) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public int PageNo { get; set; } = 1;

    public PagedResult<AuditEntry>? Results { get; private set; }

    public void OnGet()
    {
        var result = audit.Query(new QueryOptions { Search = Q ?? "", Page = PageNo, PageSize = 50 });
        if (result.Succeeded) Results = result.Value;
    }
}
