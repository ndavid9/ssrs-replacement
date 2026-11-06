using LabReport.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LabReport.Web.Pages;

public sealed class IndexModel : PageModel
{
    private readonly IReportFeed _feed;
    private readonly FileTemplateStore _templates;
    private readonly ReportJobService _jobs;
    private readonly DemoAuthorizer _authorizer;

    public IndexModel(IReportFeed feed, FileTemplateStore templates, ReportJobService jobs, DemoAuthorizer authorizer)
    {
        _feed = feed;
        _templates = templates;
        _jobs = jobs;
        _authorizer = authorizer;
    }

    public IReadOnlyList<ReportListItem> Reports { get; private set; } = [];
    public IReadOnlyList<LabReport.Core.Models.TemplateDefinition> Templates { get; private set; } = [];
    public string Provider { get; private set; } = "";

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Provider = _feed.Name;
        Reports = await _feed.ListAsync(_authorizer.TenantId, _authorizer.PrincipalId, cancellationToken);
        Templates = _templates.List();
    }

    public async Task<IActionResult> OnPostGenerateAsync(string reportId, string templateId, int samplesPerPage, CancellationToken cancellationToken)
    {
        var job = await _jobs.EnqueueAsync(new RenderRequest(reportId, templateId, samplesPerPage, null, null), cancellationToken);
        return Redirect($"/jobs/{job.Id:N}");
    }
}
