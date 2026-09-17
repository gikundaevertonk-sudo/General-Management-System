using GMS.Core.Contracts;
using GMS.Core.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GMS.Web.Pages;

public class IndexModel(DashboardService dashboard) : PageModel
{
    public DashboardSummary? Summary { get; private set; }
    public string? Error { get; private set; }

    public void OnGet()
    {
        var result = dashboard.GetSummary();
        if (result.Succeeded) Summary = result.Value;
        else Error = result.ErrorMessage;
    }
}
