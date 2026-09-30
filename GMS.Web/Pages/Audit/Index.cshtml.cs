using GMS.Core.Common;
using GMS.Core.Contracts;
using GMS.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GMS.Web.Pages.Audit;

/// <summary>
/// The audit trail, read as a list of sign-ins. Clicking one opens what that session went on to
/// do, so a question like "who sold this, and under whose login?" has a direct answer.
/// </summary>
[Authorize("perm:audit.view")]
public class IndexModel(AuditService audit) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public int PageNo { get; set; } = 1;

    /// <summary>The sign-in whose activity is expanded, if any.</summary>
    [BindProperty(SupportsGet = true)] public int? SessionId { get; set; }

    public PagedResult<LoginSessionRow>? Logins { get; private set; }
    public LoginSessionRow? OpenSession { get; private set; }
    public IReadOnlyList<SessionActivityRow> Activity { get; private set; } = Array.Empty<SessionActivityRow>();
    public string? Error { get; private set; }

    public void OnGet()
    {
        var result = audit.LoginHistory(new QueryOptions { Search = Q ?? "", Page = PageNo, PageSize = 30 });
        if (result.Failed) { Error = result.ErrorMessage; return; }
        Logins = result.Value;

        if (SessionId is not int id) return;
        OpenSession = Logins.Items.FirstOrDefault(l => l.AuditEntryId == id);

        var detail = audit.ActivityForLogin(id);
        if (detail.Succeeded) Activity = detail.Value; else Error = detail.ErrorMessage;
    }
}
