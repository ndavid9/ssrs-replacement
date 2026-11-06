using LabReport.Core.Models;
using LabReport.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LabReport.Web.Pages.Admin.Texts;

public sealed class IndexModel : PageModel
{
    private readonly ICatalogSource _catalogs;
    private readonly CatalogEditor _editor;
    private readonly IConfiguration _configuration;

    public IndexModel(ICatalogSource catalogs, CatalogEditor editor, IConfiguration configuration)
    {
        _catalogs = catalogs;
        _editor = editor;
        _configuration = configuration;
    }

    public IReadOnlyList<TextEntry> Entries { get; private set; } = [];
    public bool CanEdit { get; private set; }
    public string? Error { get; private set; }

    public void OnGet()
    {
        Entries = _catalogs.Texts().OrderBy(entry => entry.Key, StringComparer.Ordinal).ThenBy(entry => entry.VersionLabel, StringComparer.Ordinal).ToList();
        CanEdit = _catalogs.CanEdit && !string.Equals(_configuration["Data:Provider"], "Sql", StringComparison.OrdinalIgnoreCase);
    }

    public IActionResult OnPost(string key, TextScope scope, TextCategory category, string body, DateOnly effectiveFrom)
    {
        OnGet();
        if (!CanEdit)
        {
            Error = "This provider is not editing the file catalog.";
            return Page();
        }

        try
        {
            _editor.AddTextVersion(key, scope, category, body, effectiveFrom);
        }
        catch (InvalidOperationException exception)
        {
            Error = exception.Message;
            return Page();
        }

        return Redirect("/admin/texts");
    }
}
