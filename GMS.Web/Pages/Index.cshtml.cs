using GMS.Core.Abstractions;
using GMS.Core.Contracts;
using GMS.Core.Security;
using GMS.Core.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GMS.Web.Pages;

public class IndexModel(DashboardService dashboard, ICurrentUser me) : PageModel
{
    public DashboardSummary? Summary { get; private set; }
    public string? Error { get; private set; }

    /// <summary>Null for anyone who cannot run reports; the charts are sales figures by another name.</summary>
    public DashboardCharts? Charts { get; private set; }

    public void OnGet()
    {
        var result = dashboard.GetSummary();
        if (result.Succeeded) Summary = result.Value;
        else Error = result.ErrorMessage;

        if (me.HasPermission(PermissionCodes.Reports.View))
        {
            var charts = dashboard.GetCharts();
            if (charts.Succeeded) Charts = charts.Value;
        }
    }
}
