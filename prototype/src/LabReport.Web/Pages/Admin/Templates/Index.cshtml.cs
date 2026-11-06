using LabReport.Core.Models;
using LabReport.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LabReport.Web.Pages.Admin.Templates;

public sealed class IndexModel : PageModel
{
    private readonly FileTemplateStore _templates;

    public IndexModel(FileTemplateStore templates) => _templates = templates;

    public IReadOnlyList<TemplateDefinition> Templates { get; private set; } = [];

    public void OnGet() => Templates = _templates.List();
}
