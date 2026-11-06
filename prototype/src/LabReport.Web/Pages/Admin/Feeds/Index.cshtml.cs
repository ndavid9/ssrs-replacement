using LabReport.Core.Services;
using LabReport.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LabReport.Web.Pages.Admin.Feeds;

public sealed class IndexModel : PageModel
{
    private readonly FeedSettingsStore _settings;
    private readonly IReportFeed _feed;

    public IndexModel(FeedSettingsStore settings, IReportFeed feed)
    {
        _settings = settings;
        _feed = feed;
    }

    public IReadOnlyList<string> Enabled { get; private set; } = [];
    public string Provider { get; private set; } = "";
    public string? Error { get; private set; }

    public void OnGet()
    {
        Enabled = _settings.Enabled();
        Provider = _feed.Name;
    }

    public IActionResult OnPost(string[]? enabledFeeds, string? sql, string? url)
    {
        Provider = _feed.Name;
        if (!string.IsNullOrWhiteSpace(sql) || !string.IsNullOrWhiteSpace(url) || (enabledFeeds ?? []).Any(FeedContracts.LooksHostile))
        {
            Error = "Feed definitions cannot set SQL text or URLs. Choose an approved contract name.";
            Enabled = _settings.Enabled();
            return Page();
        }

        try
        {
            _settings.Save(enabledFeeds ?? []);
        }
        catch (InvalidOperationException exception)
        {
            Error = exception.Message;
            Enabled = _settings.Enabled();
            return Page();
        }

        return Redirect("/admin/feeds");
    }
}
