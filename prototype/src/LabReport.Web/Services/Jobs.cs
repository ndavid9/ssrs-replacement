using System.Text.Json;
using System.Threading.Channels;
using LabReport.Core;
using LabReport.Core.Models;
using LabReport.Core.Rendering;
using LabReport.Core.Services;

namespace LabReport.Web.Services;

public sealed record RenderRequest(string ReportId, string TemplateId, int? SamplesPerPage, string? Paper, string? Orientation);

public sealed class JobQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateBounded<Guid>(new BoundedChannelOptions(100)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = false,
        SingleWriter = false
    });

    public ValueTask EnqueueAsync(Guid id, CancellationToken cancellationToken) => _channel.Writer.WriteAsync(id, cancellationToken);

    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken) => _channel.Reader.ReadAllAsync(cancellationToken);
}

public sealed class FileJobStore
{
    private readonly AppPaths _paths;
    private readonly object _gate = new();

    public FileJobStore(AppPaths paths) => _paths = paths;

    public IReadOnlyList<JobRecord> List()
    {
        lock (_gate)
        {
            return Directory.EnumerateFiles(_paths.JobRoot, "*.json")
                .Select(path => JsonSerializer.Deserialize<JobRecord>(File.ReadAllText(path), JsonDefaults.Options)!)
                .OrderByDescending(job => job.CreatedUtc)
                .ToList();
        }
    }

    public JobRecord? Find(Guid id)
    {
        var path = JobPath(id);
        return File.Exists(path) ? JsonSerializer.Deserialize<JobRecord>(File.ReadAllText(path), JsonDefaults.Options) : null;
    }

    public JobRecord? FindReusable(string hash)
    {
        return List().FirstOrDefault(job => job.InputHash == hash && job.Status is JobStatus.Succeeded or JobStatus.Queued or JobStatus.Rendering or JobStatus.FailedValidation);
    }

    public void Save(JobRecord job)
    {
        lock (_gate)
        {
            WriteAtomic(JobPath(job.Id), JsonSerializer.Serialize(job, JsonDefaults.Options));
        }
    }

    public void SavePin(Guid id, PinnedInput pin)
    {
        lock (_gate)
        {
            WriteAtomic(PinPath(id), JsonSerializer.Serialize(pin, JsonDefaults.Options));
        }
    }

    public PinnedInput RequirePin(Guid id)
    {
        var path = PinPath(id);
        if (!File.Exists(path))
        {
            throw new InvalidOperationException("The pinned input for this job is missing.");
        }

        return JsonSerializer.Deserialize<PinnedInput>(File.ReadAllText(path), JsonDefaults.Options)
            ?? throw new InvalidOperationException("The pinned input could not be read.");
    }

    public void SaveArtifact(Guid id, byte[] pdf)
    {
        var path = ArtifactPath(id);
        Directory.CreateDirectory(_paths.ArtifactRoot);
        var temporary = path + ".tmp";
        File.WriteAllBytes(temporary, pdf);
        File.Move(temporary, path, overwrite: true);
    }

    public string? ExistingArtifact(Guid id)
    {
        var path = ArtifactPath(id);
        return File.Exists(path) ? path : null;
    }

    public void Audit(string action, Guid? id, string detail)
    {
        var line = JsonSerializer.Serialize(new
        {
            utc = DateTimeOffset.UtcNow,
            action,
            jobId = id,
            detail
        }, JsonDefaults.Options);
        lock (_gate)
        {
            File.AppendAllText(_paths.AuditPath, line + "\n");
        }
    }

    private string JobPath(Guid id) => PathSafety.CombineUnderRoot(_paths.JobRoot, id.ToString("N") + ".json");
    private string PinPath(Guid id) => PathSafety.CombineUnderRoot(_paths.PinRoot, id.ToString("N") + ".json");
    private string ArtifactPath(Guid id) => PathSafety.CombineUnderRoot(_paths.ArtifactRoot, id.ToString("N") + ".pdf");

    private static void WriteAtomic(string path, string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, json);
        File.Move(temporary, path, overwrite: true);
    }
}

public interface IPostRenderSigner
{
    Task<SigningHookResult> AfterRenderAsync(byte[] pdf, CancellationToken cancellationToken);
}

public sealed record SigningHookResult(bool CryptographicSignatureApplied, string Note);

public sealed class NoCryptographicSigner : IPostRenderSigner
{
    public Task<SigningHookResult> AfterRenderAsync(byte[] pdf, CancellationToken cancellationToken) =>
        Task.FromResult(new SigningHookResult(false, "No cryptographic signature was applied. The signatory block is a visual label selected by id. A production hook can detached-sign these PDF bytes after this point."));
}

public sealed class ReportJobService
{
    private readonly IReportFeed _feed;
    private readonly FileTemplateStore _templates;
    private readonly ICatalogSource _catalogs;
    private readonly FileJobStore _jobs;
    private readonly JobQueue _queue;
    private readonly DemoAuthorizer _authorizer;
    private readonly SignatoryAssets _assets;

    public ReportJobService(
        IReportFeed feed,
        FileTemplateStore templates,
        ICatalogSource catalogs,
        FileJobStore jobs,
        JobQueue queue,
        DemoAuthorizer authorizer,
        SignatoryAssets assets)
    {
        _feed = feed;
        _templates = templates;
        _catalogs = catalogs;
        _jobs = jobs;
        _queue = queue;
        _authorizer = authorizer;
        _assets = assets;
    }

    public async Task<JobRecord> EnqueueAsync(RenderRequest request, CancellationToken cancellationToken)
    {
        if (!_authorizer.Allow(_authorizer.TenantId, _authorizer.PrincipalId))
        {
            return Failed(request, "The demo authorizer refused the configured principal.", []);
        }

        var template = _templates.Find(request.TemplateId);
        if (template is null)
        {
            return Failed(request, "Unknown template.", [new("template.missing", "The template id is not in the saved catalog.")]);
        }

        var effective = template with
        {
            SamplesPerPage = request.SamplesPerPage ?? template.SamplesPerPage,
            Paper = string.IsNullOrWhiteSpace(request.Paper) ? template.Paper : request.Paper,
            Orientation = string.IsNullOrWhiteSpace(request.Orientation) ? template.Orientation : request.Orientation
        };
        ReportSnapshot? snapshot;
        try
        {
            snapshot = await _feed.GetAsync(_authorizer.TenantId, request.ReportId, _authorizer.PrincipalId, cancellationToken);
        }
        catch (Exception exception) when (exception is InvalidOperationException or Microsoft.Data.SqlClient.SqlException)
        {
            return Failed(request, exception.Message, [new("feed.denied", exception.Message)]);
        }

        if (snapshot is null)
        {
            return Failed(request, "Report was not found for the demo tenant.", [new("report.missing", "No snapshot exists for that report id.")]);
        }

        var texts = _catalogs.Texts();
        var mrls = _catalogs.Mrls();
        var signatory = _catalogs.Signatory(effective.SignatoryId);
        var issues = new List<ValidationIssue>();
        if (effective.Sections.Any(section => section.Enabled && section.Type == "approval-signature"))
        {
            if (signatory is null || !_assets.IsApproved(signatory.AssetKey))
            {
                issues.Add(new("signatory.asset", "The signatory asset key is not in the developer whitelist."));
            }
        }

        var plan = RenderPlanner.Plan(snapshot, effective, texts, mrls, signatory, isPreview: false);
        issues.AddRange(plan.Issues);
        var pin = new PinnedInput
        {
            SchemaVersion = "1.0",
            Snapshot = plan.Snapshot,
            Template = effective,
            Texts = RenderPlanner.TextsUsed(plan, texts, effective.Language),
            Mrls = mrls.OrderBy(limit => limit.MrlId, StringComparer.Ordinal).ToList(),
            Signatory = signatory,
            SamplesPerPage = effective.SamplesPerPage,
            Paper = effective.Paper,
            Orientation = effective.Orientation
        };
        var hash = SnapshotHasher.Hash(pin);
        if (issues.Count > 0)
        {
            var existingInvalid = _jobs.FindReusable(hash);
            if (existingInvalid is { Status: JobStatus.FailedValidation })
            {
                _jobs.Audit("deduplicated", existingInvalid.Id, hash);
                return existingInvalid;
            }

            var invalid = NewJob(request, effective, hash, JobStatus.FailedValidation, issues, "Validation failed. No PDF was published.");
            _jobs.Save(invalid);
            _jobs.SavePin(invalid.Id, pin);
            _jobs.Audit("validation_failed", invalid.Id, invalid.Error ?? "");
            return invalid;
        }

        var existing = _jobs.FindReusable(hash);
        if (existing is not null)
        {
            _jobs.Audit("deduplicated", existing.Id, hash);
            return existing with { DeduplicatedFrom = existing.Id };
        }

        var job = NewJob(request, effective, hash, JobStatus.Queued, [], null);
        _jobs.Save(job);
        _jobs.SavePin(job.Id, pin);
        await _queue.EnqueueAsync(job.Id, cancellationToken);
        _jobs.Audit("enqueued", job.Id, hash);
        return job;
    }

    private JobRecord Failed(RenderRequest request, string error, IReadOnlyList<ValidationIssue> issues)
    {
        var job = new JobRecord
        {
            Id = Guid.NewGuid(),
            TenantId = _authorizer.TenantId,
            ReportId = request.ReportId,
            TemplateId = request.TemplateId,
            TemplateVersion = 0,
            InputHash = "",
            Status = JobStatus.FailedValidation,
            Attempt = 0,
            CreatedUtc = DateTimeOffset.UtcNow,
            FinishedUtc = DateTimeOffset.UtcNow,
            Error = error,
            Issues = issues
        };
        _jobs.Save(job);
        _jobs.Audit("validation_failed", job.Id, error);
        return job;
    }

    private JobRecord NewJob(RenderRequest request, TemplateDefinition template, string hash, JobStatus status, IReadOnlyList<ValidationIssue> issues, string? error) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = _authorizer.TenantId,
            ReportId = request.ReportId,
            TemplateId = template.TemplateId,
            TemplateVersion = template.Version,
            InputHash = hash,
            Status = status,
            Attempt = 0,
            CreatedUtc = DateTimeOffset.UtcNow,
            FinishedUtc = status == JobStatus.FailedValidation ? DateTimeOffset.UtcNow : null,
            Error = error,
            Issues = issues
        };
}

public sealed class RenderWorker : BackgroundService
{
    private readonly JobQueue _queue;
    private readonly FileJobStore _jobs;
    private readonly ReportDocumentRenderer _renderer;
    private readonly IPostRenderSigner _signer;
    private readonly ILogger<RenderWorker> _logger;
    private readonly int _concurrency;
    private readonly int _maxAttempts;
    private readonly int _timeoutSeconds;

    public RenderWorker(
        JobQueue queue,
        FileJobStore jobs,
        ReportDocumentRenderer renderer,
        IPostRenderSigner signer,
        IConfiguration configuration,
        ILogger<RenderWorker> logger)
    {
        _queue = queue;
        _jobs = jobs;
        _renderer = renderer;
        _signer = signer;
        _logger = logger;
        _concurrency = Math.Clamp(configuration.GetValue("Worker:MaxConcurrency", 2), 1, 4);
        _maxAttempts = Math.Clamp(configuration.GetValue("Worker:MaxAttempts", 3), 1, 5);
        _timeoutSeconds = Math.Clamp(configuration.GetValue("Worker:RenderTimeoutSeconds", 90), 15, 180);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        foreach (var job in _jobs.List().Where(job => job.Status is JobStatus.Queued or JobStatus.Rendering))
        {
            _jobs.Save(job with { Status = JobStatus.Queued, Error = null });
            await _queue.EnqueueAsync(job.Id, stoppingToken);
        }

        var pumps = Enumerable.Range(0, _concurrency).Select(_ => PumpAsync(stoppingToken));
        await Task.WhenAll(pumps);
    }

    private async Task PumpAsync(CancellationToken stoppingToken)
    {
        await foreach (var id in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessAsync(id, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Job {JobId} failed outside the retry handler.", id);
            }
        }
    }

    private async Task ProcessAsync(Guid id, CancellationToken stoppingToken)
    {
        var job = _jobs.Find(id);
        if (job is null || job.Status != JobStatus.Queued)
        {
            return;
        }

        if (job.CancelRequested)
        {
            _jobs.Save(job with { Status = JobStatus.Cancelled, FinishedUtc = DateTimeOffset.UtcNow, Error = "Cancelled before render." });
            _jobs.Audit("cancelled", id, "before-render");
            return;
        }

        var attempt = job.Attempt + 1;
        _jobs.Save(job with { Status = JobStatus.Rendering, Attempt = attempt, Error = null });
        _jobs.Audit("render_started", id, "attempt " + attempt.ToString(System.Globalization.CultureInfo.InvariantCulture));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_timeoutSeconds));
        try
        {
            var pin = _jobs.RequirePin(id);
            var latest = _jobs.Find(id);
            if (latest?.CancelRequested == true)
            {
                _jobs.Save(latest with { Status = JobStatus.Cancelled, FinishedUtc = DateTimeOffset.UtcNow, Error = "Cancelled." });
                _jobs.Audit("cancelled", id, "before-pdf");
                return;
            }

            var plan = RenderPlanner.Plan(pin.Snapshot, pin.Template, pin.Texts, pin.Mrls, pin.Signatory, isPreview: false);
            if (!plan.IsValid)
            {
                _jobs.Save(job with
                {
                    Status = JobStatus.FailedValidation,
                    Attempt = attempt,
                    FinishedUtc = DateTimeOffset.UtcNow,
                    Issues = plan.Issues,
                    Error = "Pinned input failed validation."
                });
                return;
            }

            var pdf = await _renderer.RenderPdfAsync(plan, timeout.Token);
            var signing = await _signer.AfterRenderAsync(pdf, timeout.Token);
            _jobs.SaveArtifact(id, pdf);
            _jobs.Save((_jobs.Find(id) ?? job) with
            {
                Status = JobStatus.Succeeded,
                Attempt = attempt,
                FinishedUtc = DateTimeOffset.UtcNow,
                PdfHash = SnapshotHasher.HashBytes(pdf),
                Error = null,
                SigningNote = signing.Note,
                CryptographicSignatureApplied = signing.CryptographicSignatureApplied
            });
            _jobs.Audit("render_succeeded", id, SnapshotHasher.HashBytes(pdf));
        }
        catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
        {
            await RetryOrFailAsync(id, attempt, "The render timed out or was cancelled.");
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "Render attempt {Attempt} failed for {JobId}.", attempt, id);
            await RetryOrFailAsync(id, attempt, exception.Message);
        }
    }

    private async Task RetryOrFailAsync(Guid id, int attempt, string error)
    {
        var job = _jobs.Find(id);
        if (job is null)
        {
            return;
        }

        if (job.CancelRequested)
        {
            _jobs.Save(job with { Status = JobStatus.Cancelled, FinishedUtc = DateTimeOffset.UtcNow, Error = error });
            _jobs.Audit("cancelled", id, error);
            return;
        }

        if (attempt < _maxAttempts)
        {
            _jobs.Save(job with { Status = JobStatus.Queued, Attempt = attempt, Error = error });
            _jobs.Audit("retry", id, error);
            await Task.Delay(TimeSpan.FromSeconds(Math.Min(attempt, 3)));
            await _queue.EnqueueAsync(id, CancellationToken.None);
            return;
        }

        _jobs.Save(job with { Status = JobStatus.Failed, Attempt = attempt, FinishedUtc = DateTimeOffset.UtcNow, Error = error });
        _jobs.Audit("render_failed", id, error);
    }
}
