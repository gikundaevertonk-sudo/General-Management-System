using GMS.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GMS.Web.Pages.Settings;

[Authorize("perm:settings.manage")]
public class IndexModel(SettingsService settings) : PageModel
{
    [BindProperty] public string CompanyName { get; set; } = "";
    [BindProperty] public string CurrencyCode { get; set; } = "";
    [BindProperty] public decimal DefaultTaxRatePercent { get; set; }
    [BindProperty] public bool LowStockScanEnabled { get; set; }
    public string? Error { get; private set; }

    public void OnGet() => Load();

    public IActionResult OnPost()
    {
        var results = new[]
        {
            settings.SetValue(SettingKeys.CompanyName, CompanyName),
            settings.SetValue(SettingKeys.CurrencyCode, CurrencyCode),
            settings.SetValue(SettingKeys.DefaultTaxRatePercent, DefaultTaxRatePercent.ToString("0.####")),
            settings.SetValue(SettingKeys.LowStockScanEnabled, LowStockScanEnabled ? "true" : "false"),
        };
        var failed = results.FirstOrDefault(r => r.Failed);
        if (failed is not null) { Error = failed.ErrorMessage; return Page(); }

        TempData["Flash"] = "Settings saved.";
        return RedirectToPage();
    }

    private void Load()
    {
        CompanyName = settings.GetString(SettingKeys.CompanyName, "My Business");
        CurrencyCode = settings.GetString(SettingKeys.CurrencyCode, "USD");
        DefaultTaxRatePercent = settings.GetDecimal(SettingKeys.DefaultTaxRatePercent, 0);
        LowStockScanEnabled = settings.GetBool(SettingKeys.LowStockScanEnabled, true);
    }
}
