using System.Net;
using LabReport.Core.Rendering;
using LabReport.Core.Services;
using LabReport.Web.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();
builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<AppPaths>();
builder.Services.AddSingleton<FileTemplateStore>();
builder.Services.AddSingleton<FileCatalogStore>();
builder.Services.AddSingleton<FileJobStore>();
builder.Services.AddSingleton<FeedSettingsStore>();
builder.Services.AddSingleton<JobQueue>();
builder.Services.AddSingleton<DemoAuthorizer>();
builder.Services.AddSingleton<SignatoryAssets>();
builder.Services.AddSingleton<ReportFontAsset>();
builder.Services.AddSingleton<PdfInsertCatalog>();
builder.Services.AddSingleton<ReportPreparation>();
builder.Services.AddSingleton<HtmlDocumentComposer>();
builder.Services.AddSingleton<RazorReportRenderer>();
builder.Services.AddSingleton<PlaywrightPdfRenderer>();
builder.Services.AddSingleton<ReportDocumentRenderer>();
builder.Services.AddSingleton<JsonReportFeed>();
builder.Services.AddSingleton<CatalogEditor>();
builder.Services.AddSingleton<ReportJobService>();
builder.Services.AddSingleton<ArtifactExporter>();
builder.Services.AddSingleton<IPostRenderSigner, NoCryptographicSigner>();
builder.Services.AddHostedService<RenderWorker>();

var provider = builder.Configuration["Data:Provider"] ?? "Json";
if (string.Equals(provider, "Sql", StringComparison.OrdinalIgnoreCase))
{
    var connectionString = builder.Configuration.GetConnectionString("ReportDb")
        ?? throw new InvalidOperationException("ConnectionStrings:ReportDb is required when Data:Provider is Sql.");
    builder.Services.AddSingleton(new SqlReportFeed(connectionString));
    builder.Services.AddSingleton<IReportFeed>(services => services.GetRequiredService<SqlReportFeed>());
    builder.Services.AddSingleton<ICatalogSource, SqlCatalogSource>();
}
else
{
    builder.Services.AddSingleton<IReportFeed>(services => services.GetRequiredService<JsonReportFeed>());
    builder.Services.AddSingleton<ICatalogSource>(services => services.GetRequiredService<FileCatalogStore>());
}

var app = builder.Build();
SeedBootstrap.Ensure(app.Services.GetRequiredService<AppPaths>());

if (args.Contains("--export"))
{
    var exportIndex = Array.IndexOf(args, "--export");
    var directory = args.Length > exportIndex + 1 && !args[exportIndex + 1].StartsWith('-')
        ? args[exportIndex + 1]
        : "/opt/cursor/artifacts";
    await app.StartAsync();
    try
    {
        await app.Services.GetRequiredService<ArtifactExporter>().ExportAsync(directory, CancellationToken.None);
    }
    finally
    {
        await app.StopAsync();
    }

    return;
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
}

app.UseStaticFiles();
app.MapRazorPages();
app.MapGet("/health", (IReportFeed feed) => Results.Ok(new { status = "ok", provider = feed.Name }));
app.MapGet("/error", () => Results.Problem("The demo hit an unexpected error."));
app.MapGet("/preview/{reportId}", async (string reportId, string? templateId, int? samplesPerPage, IReportFeed feed, FileTemplateStore templates, ICatalogSource catalogs, DemoAuthorizer authorizer, SignatoryAssets assets, ReportDocumentRenderer renderer, CancellationToken cancellationToken) =>
{
    var template = templates.Find(string.IsNullOrWhiteSpace(templateId) ? "analytical-default" : templateId);
    if (template is null)
    {
        return Results.NotFound();
    }

    var effective = template with { SamplesPerPage = samplesPerPage ?? template.SamplesPerPage };
    var snapshot = await feed.GetAsync(authorizer.TenantId, reportId, authorizer.PrincipalId, cancellationToken);
    if (snapshot is null)
    {
        return Results.NotFound();
    }

    var signatory = catalogs.Signatory(effective.SignatoryId);
    var plan = app.Services.GetRequiredService<ReportPreparation>().Prepare(snapshot, effective, catalogs.Texts(), catalogs.Mrls(), signatory, isPreview: true).Plan;
    if (!plan.IsValid)
    {
        var items = string.Join("", plan.Issues.Select(issue => "<li>" + WebUtility.HtmlEncode(issue.Code + ": " + issue.Message) + "</li>"));
        return Results.Content("<!DOCTYPE html><html><body><h1>Preview blocked</h1><p>Validation failed, so this preview was not published.</p><ul>" + items + "</ul></body></html>", "text/html");
    }

    var html = await renderer.RenderHtmlAsync(plan, cancellationToken);
    return Results.Content(html, "text/html");
});
app.MapGet("/jobs/{id}/html", async (string id, FileJobStore jobs, ReportDocumentRenderer renderer, CancellationToken cancellationToken) =>
{
    if (!Guid.TryParse(id, out var guid))
    {
        return Results.NotFound();
    }

    var job = jobs.Find(guid);
    if (job is null || job.Status != LabReport.Core.Models.JobStatus.Succeeded)
    {
        return Results.BadRequest("Pinned HTML is available after a successful render.");
    }

    var pin = jobs.RequirePin(guid);
    var plan = app.Services.GetRequiredService<ReportPreparation>().Prepare(pin.Snapshot, pin.Template, pin.Texts, pin.Mrls, pin.Signatory, isPreview: false).Plan;
    if (!plan.IsValid)
    {
        return Results.BadRequest("The pinned input no longer passes the current planner.");
    }

    return Results.Content(await renderer.RenderHtmlAsync(plan, cancellationToken), "text/html");
});
app.MapGet("/artifacts/{id}", (string id, FileJobStore jobs) =>
{
    if (!Guid.TryParse(id, out var guid))
    {
        return Results.NotFound();
    }

    var job = jobs.Find(guid);
    var path = jobs.ExistingArtifact(guid);
    if (job is null || job.Status != LabReport.Core.Models.JobStatus.Succeeded || path is null)
    {
        return Results.NotFound();
    }

    return Results.File(path, "application/pdf", "synthetic-" + job.ReportId + ".pdf");
});

app.Run();

public partial class Program;
