using LabReport.Core.Models;
using LabReport.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LabReport.Web.Pages.Jobs;

public sealed class IndexModel : PageModel
{
    private readonly FileJobStore _jobs;

    public IndexModel(FileJobStore jobs) => _jobs = jobs;

    public IReadOnlyList<JobRecord> Jobs { get; private set; } = [];

    public void OnGet() => Jobs = _jobs.List();
}
