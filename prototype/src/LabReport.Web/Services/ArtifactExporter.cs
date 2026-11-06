using LabReport.Core.Models;
using LabReport.Core.Services;

namespace LabReport.Web.Services;

public sealed class ArtifactExporter
{
    private readonly ReportJobService _jobs;
    private readonly FileJobStore _store;
    private readonly FileTemplateStore _templates;

    public ArtifactExporter(ReportJobService jobs, FileJobStore store, FileTemplateStore templates)
    {
        _jobs = jobs;
        _store = store;
        _templates = templates;
    }

    public async Task ExportAsync(string directory, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(directory);
        await ProduceAsync(directory, "RPT-DEMO-0001", "analytical-default", 2, "demo-report-without-mrl.pdf", cancellationToken);
        var source = _templates.Require("analytical-default");
        if (_templates.Find("analytical-with-mrl") is null)
        {
            var added = TemplateEditor.AddSection(source, "mrl-comparison") with
            {
                TemplateId = "analytical-with-mrl",
                Name = "Analytical demonstration with fictional limits"
            };
            _templates.Save(added);
        }

        await ProduceAsync(directory, "RPT-DEMO-0001", "analytical-with-mrl", 2, "demo-report-with-mrl.pdf", cancellationToken);
        await ProduceAsync(directory, "RPT-DEMO-STRESS", "analytical-stress", 2, "demo-report-stress.pdf", cancellationToken);
        await ProduceAsync(directory, "RPT-DEMO-0001", "analytical-default", 1, "demo-report-groups-1.pdf", cancellationToken);
        await ProduceAsync(directory, "RPT-DEMO-0001", "analytical-default", 3, "demo-report-groups-3.pdf", cancellationToken);
    }

    private async Task ProduceAsync(string directory, string reportId, string templateId, int samplesPerPage, string fileName, CancellationToken cancellationToken)
    {
        var job = await _jobs.EnqueueAsync(new RenderRequest(reportId, templateId, samplesPerPage, null, null), cancellationToken);
        var finished = await WaitAsync(job.Id, cancellationToken);
        if (finished.Status != JobStatus.Succeeded)
        {
            throw new InvalidOperationException($"Export {fileName} ended as {finished.Status}: {finished.Error}");
        }

        var source = _store.ExistingArtifact(finished.Id) ?? throw new InvalidOperationException("PDF missing after success.");
        File.Copy(source, Path.Combine(directory, fileName), overwrite: true);
    }

    private async Task<JobRecord> WaitAsync(Guid id, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 180; attempt++)
        {
            var job = _store.Find(id) ?? throw new InvalidOperationException("Job disappeared.");
            if (job.Status is JobStatus.Succeeded or JobStatus.Failed or JobStatus.FailedValidation or JobStatus.Cancelled)
            {
                return job;
            }

            await Task.Delay(500, cancellationToken);
        }

        throw new TimeoutException("Timed out waiting for " + id);
    }
}
