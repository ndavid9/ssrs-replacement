using LabReport.Core.Models;
using LabReport.Core.Pdf;
using LabReport.Core.Rendering;
using LabReport.Core.Services;

namespace LabReport.Web.Services;

public sealed class ReportFontAsset
{
    public ReportFontAsset(IWebHostEnvironment environment)
    {
        Regular = File.ReadAllBytes(Path.Combine(environment.ContentRootPath, "Assets", "fonts", "LiberationSans-Regular.ttf"));
        ReportFonts.Ensure(Regular);
    }

    public byte[] Regular { get; }
}

public sealed record InsertInspection(
    IReadOnlyList<ValidationIssue> Issues,
    IReadOnlyList<ReportInsert> Inserts,
    IReadOnlyList<InsertDigest> Digests);

public sealed class PdfInsertCatalog
{
    private readonly string _root;

    public PdfInsertCatalog(IWebHostEnvironment environment) =>
        _root = Path.Combine(environment.ContentRootPath, "Seed", "inserts");

    public InsertInspection Inspect(TemplateDefinition template)
    {
        var issues = new List<ValidationIssue>();
        var inserts = new List<ReportInsert>();
        var digests = new List<InsertDigest>();
        foreach (var insert in template.Inserts)
        {
            if (string.IsNullOrWhiteSpace(insert.File) || insert.File.Contains('/') || insert.File.Contains('\\') || insert.File.Contains("..", StringComparison.Ordinal))
            {
                continue;
            }

            string path;
            try
            {
                path = PathSafety.CombineUnderRoot(_root, insert.File);
            }
            catch (InvalidOperationException)
            {
                issues.Add(new("insert.file", $"Insert '{insert.Id}' is not a file inside the inserts folder."));
                continue;
            }

            if (!File.Exists(path))
            {
                issues.Add(new("insert.missing", $"Insert '{insert.Id}' file '{insert.File}' was not found in Seed/inserts."));
                continue;
            }

            var bytes = File.ReadAllBytes(path);
            var sha = SnapshotHasher.HashBytes(bytes);
            var inspection = PdfInsertInspector.Inspect(bytes);
            if (inspection is not PdfInspection.Ok ok)
            {
                var rejected = (PdfInspection.Rejected)inspection;
                issues.Add(new(rejected.Code, "Insert '" + insert.Id + "': " + PdfInsertInspector.Describe(rejected, sha)));
                digests.Add(new InsertDigest
                {
                    Id = insert.Id,
                    File = insert.File,
                    Sha256 = sha,
                    PageCount = 0,
                    Accepted = false
                });
                continue;
            }

            digests.Add(new InsertDigest
            {
                Id = insert.Id,
                File = insert.File,
                Sha256 = sha,
                PageCount = ok.PageCount,
                Accepted = true
            });
            inserts.Add(new ReportInsert
            {
                Id = insert.Id,
                Label = insert.Label,
                Mode = insert.Mode,
                Slot = insert.Slot,
                FileName = insert.File,
                Sha256 = sha,
                PageCount = ok.PageCount,
                Pages = ok.Pages
            });
        }

        return new InsertInspection(issues, inserts, digests);
    }

    public IReadOnlyList<InsertPayload> PayloadsFor(RenderPlan plan)
    {
        var payloads = new List<InsertPayload>();
        foreach (var insert in plan.Inserts)
        {
            var path = PathSafety.CombineUnderRoot(_root, insert.FileName);
            var bytes = File.ReadAllBytes(path);
            var sha = SnapshotHasher.HashBytes(bytes);
            if (!string.Equals(sha, insert.Sha256, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Insert '{insert.Id}' changed after the job was pinned.");
            }

            payloads.Add(new InsertPayload(insert.Id, insert.Mode, insert.Label, insert.PageCount, bytes));
        }

        return payloads;
    }
}

public sealed record PreparedPlan(RenderPlan Plan, InsertInspection Inspection);

public sealed class ReportPreparation
{
    private readonly PdfInsertCatalog _catalog;

    public ReportPreparation(PdfInsertCatalog catalog) => _catalog = catalog;

    public PreparedPlan Prepare(
        ReportSnapshot snapshot,
        TemplateDefinition template,
        IReadOnlyList<TextEntry> texts,
        IReadOnlyList<MrlLimit> mrls,
        SignatoryBlock? signatory,
        bool isPreview)
    {
        var inspection = _catalog.Inspect(template);
        var plan = RenderPlanner.Plan(snapshot, template, texts, mrls, signatory, isPreview, inspection.Inserts, inspection.Issues);
        return new PreparedPlan(plan, inspection);
    }

    public IReadOnlyList<InsertPayload> PayloadsFor(RenderPlan plan) => _catalog.PayloadsFor(plan);
}

public static class ReportFinish
{
    public static async Task<SigningHookResult> FinishAsync(
        byte[] rendered,
        IReadOnlyList<InsertPayload> payloads,
        byte[] font,
        IPostRenderSigner signer,
        CancellationToken cancellationToken)
    {
        var merged = PdfInsertComposer.Merge(rendered, payloads);
        var stamped = PdfInsertComposer.Stamp(merged.Pdf, merged.Plan, font);
        return await signer.AfterRenderAsync(stamped, cancellationToken);
    }
}
