using GMS.Core.Models;
using GMS.Core.Security;
using GMS.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GMS.Web.Pages.Notifications;

// No [Authorize("perm:...")]: every signed-in user may see their own notifications.
public class IndexModel(NotificationService notifications, GMS.Core.Abstractions.ICurrentUser me) : PageModel
{
    [BindProperty(SupportsGet = true)] public bool UnreadOnly { get; set; }

    public IReadOnlyList<Notification> Items { get; private set; } = [];
    public bool CanScan { get; private set; }

    public void OnGet()
    {
        Items = notifications.ListForCurrentUser(UnreadOnly, take: 100);
        CanScan = me.HasPermission(PermissionCodes.Inventory.View);
    }

    public IActionResult OnPostMarkRead(int id)
    {
        notifications.MarkRead(id);
        return RedirectToPage(new { UnreadOnly });
    }

    public IActionResult OnPostMarkAllRead()
    {
        notifications.MarkAllReadForCurrentUser();
        return RedirectToPage(new { UnreadOnly });
    }

    public IActionResult OnPostScan()
    {
        var result = notifications.RunLowStockScan();
        TempData["Flash"] = result.Succeeded ? $"Scan complete — {result.Value} new alert(s)." : result.ErrorMessage;
        return RedirectToPage(new { UnreadOnly });
    }
}
