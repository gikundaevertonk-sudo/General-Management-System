using System.Text.Json;
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

            // Only offer what is actually downloadable. Editing this file is how a release is
            // published, so it is easy to add an entry before copying the installer to the server -
            // and then the page advertises the current version with a link that 404s, which looks
            // far worse than not offering it yet. A release whose file is missing is skipped and
            // logged; the entry can stay in the manifest and starts being offered the moment the
            // file appears, with no further edit.
            var all = manifest?.Releases ?? [];
            var missing = all.Where(r => !IsDownloadable(r)).ToList();
            foreach (var r in missing)
                logger.LogWarning("Release {Version} is in releases.json but {Url} is not on the server, so it is not being offered.",
                    r.Version, r.Url);

            Releases = all.Except(missing).ToList();
            if (Releases.Count == 0) Error = "No releases have been published yet.";
        }
        catch (JsonException ex)
        {
            // A malformed manifest is an operator mistake, not something to show a customer.
            logger.LogError(ex, "releases.json could not be parsed");
            Error = "The download list is temporarily unavailable.";
        }
    }

    /// <summary>
    /// Whether this release's installer can actually be fetched.
    /// </summary>
    /// <remarks>
    /// An absolute URL is taken on trust: it points at storage or a CDN this site cannot see, and
    /// reaching out over the network to check would make every page load wait on someone else. A
    /// site-relative URL is served from wwwroot, so it can be checked properly, and that is the
    /// form that gets forgotten - the file is large, it is not in git, and it has to be copied to
    /// the server by hand.
    /// </remarks>
    private bool IsDownloadable(Release release)
    {
        if (string.IsNullOrWhiteSpace(release.Url)) return false;
        if (Uri.TryCreate(release.Url, UriKind.Absolute, out var absolute) &&
            (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps))
        {
            return true;
        }

        var relative = release.Url.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar);
        var webRoot = environment.WebRootPath;
        if (string.IsNullOrEmpty(webRoot)) return false;
        return System.IO.File.Exists(Path.Combine(webRoot, relative));
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

    }
}
