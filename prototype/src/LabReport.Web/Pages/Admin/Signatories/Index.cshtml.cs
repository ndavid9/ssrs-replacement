using LabReport.Core.Models;
using LabReport.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LabReport.Web.Pages.Admin.Signatories;

public sealed class IndexModel : PageModel
{
    private readonly ICatalogSource _catalogs;
    private readonly SignatoryAssets _assets;

    public IndexModel(ICatalogSource catalogs, SignatoryAssets assets)
    {
        _catalogs = catalogs;
        _assets = assets;
    }

    public IReadOnlyList<SignatoryBlock> Signatories { get; private set; } = [];
    public SignatoryAssets Assets => _assets;

    public void OnGet() => Signatories = _catalogs.Signatories();
}
