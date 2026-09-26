using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GMS.Web.Pages;

[AllowAnonymous]
public class DownloadModel(IWebHostEnvironment environment, ILogger<DownloadModel> logger) : PageModel
{
    public IReadOnlyList<Release> Releases { get; private set; } = [];
    public string? Error { get; private set; }

    public Release? Latest => Releases.FirstOrDefault();
    public IEnumerable<Release> Previous => Releases.Skip(1);

    public void OnGet()
    {
        // Read per request rather than cached at start-up: publishing a release is editing
        // this file, and it should not need the site restarted to take effect.
        var path = Path.Combine(environment.ContentRootPath, "releases.json");
        if (!System.IO.File.Exists(path))
        {
            Error = "No releases have been published yet.";
            return;
        }

        try
        {
            var manifest = JsonSerializer.Deserialize<Manifest>(
                System.IO.File.ReadAllText(path),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            Releases = manifest?.Releases ?? [];
            if (Releases.Count == 0) Error = "No releases have been published yet.";
        }
        catch (JsonException ex)
        {
            // A malformed manifest is an operator mistake, not something to show a customer.
            logger.LogError(ex, "releases.json could not be parsed");
            Error = "The download list is temporarily unavailable.";
        }
    }

    public sealed class Manifest
    {
        public string Product { get; set; } = "";
        public List<Release> Releases { get; set; } = [];
    }

    public sealed class Release
    {
        public string Version { get; set; } = "";
        public string Released { get; set; } = "";
        public string Url { get; set; } = "";
        public long SizeBytes { get; set; }
        public string? Sha256 { get; set; }
        public List<string> Notes { get; set; } = [];

        public string SizeDisplay => SizeBytes <= 0 ? "" : $"{SizeBytes / 1024d / 1024d:0.#} MB";

        public string ReleasedDisplay =>
            DateTime.TryParse(Released, out var d) ? d.ToString("d MMMM yyyy") : Released;

        [JsonIgnore]
        public bool HasFile => !string.IsNullOrWhiteSpace(Url);
    }
}
