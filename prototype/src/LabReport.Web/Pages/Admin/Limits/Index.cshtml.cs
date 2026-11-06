using LabReport.Core.Models;
using LabReport.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LabReport.Web.Pages.Admin.Limits;

public sealed class IndexModel : PageModel
{
    private readonly ICatalogSource _catalogs;
    private readonly CatalogEditor _editor;

    public IndexModel(ICatalogSource catalogs, CatalogEditor editor)
    {
        _catalogs = catalogs;
        _editor = editor;
    }

    public IReadOnlyList<MrlLimit> Limits { get; private set; } = [];
    public IReadOnlyList<string> Analytes { get; private set; } = [];
    public bool CanEdit { get; private set; }
    public string? Error { get; private set; }

    public void OnGet()
    {
        Limits = _catalogs.Mrls().OrderBy(limit => limit.MrlId, StringComparer.Ordinal).ToList();
        CanEdit = _catalogs.CanEdit;
        Analytes = CanEdit ? _editor.KnownAnalyteCodes() : [];
    }

    public IActionResult OnPost(string analyteCode, string jurisdiction, string matrix, string commodity, string unit, decimal limitValue, DateOnly effectiveFrom, string sourceName, bool fictionalConfirmed)
    {
        OnGet();
        if (!CanEdit)
        {
            Error = "Limits are read from SQL in this mode.";
            return Page();
        }

        try
        {
            _editor.AddFictionalLimit(analyteCode, jurisdiction, matrix, commodity, unit, limitValue, effectiveFrom, sourceName, fictionalConfirmed);
        }
        catch (InvalidOperationException exception)
        {
            Error = exception.Message;
            return Page();
        }

        return Redirect("/admin/limits");
    }
}
