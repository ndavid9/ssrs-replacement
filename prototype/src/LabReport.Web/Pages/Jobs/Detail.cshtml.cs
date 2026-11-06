using LabReport.Core.Models;
using LabReport.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LabReport.Web.Pages.Jobs;

public sealed class DetailModel : PageModel
{
    private readonly FileJobStore _jobs;

    public DetailModel(FileJobStore jobs) => _jobs = jobs;

    public JobRecord? Job { get; private set; }

    public IActionResult OnGet(string id)
    {
        if (!Guid.TryParse(id, out var guid))
        {
            return NotFound();
        }

        Job = _jobs.Find(guid);
        if (Job is null)
        {
            return NotFound();
        }

        if (Job.Status is JobStatus.Queued or JobStatus.Rendering)
        {
            Response.Headers.Append("Refresh", "2");
        }

        return Page();
    }

    public IActionResult OnPostCancel(string id)
    {
        if (!Guid.TryParse(id, out var guid))
        {
            return NotFound();
        }

        var job = _jobs.Find(guid);
        if (job is null)
        {
            return NotFound();
        }

        if (job.Status is JobStatus.Queued or JobStatus.Rendering)
        {
            _jobs.Save(job with { CancelRequested = true });
            _jobs.Audit("cancel_requested", guid, "from the desk");
        }

        return Redirect($"/jobs/{guid:N}");
    }
}
