using System.Text.RegularExpressions;
using LabReport.Core.Models;
using LabReport.Core.Pdf;
using LabReport.Core.Rendering;
using LabReport.Core.Services;
using LabReport.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LabReport.Tests;

public class InsertTests
{
    private static byte[] Font() =>
        File.ReadAllBytes(Path.Combine(DeskFactory.FindContentRoot(), "Assets", "fonts", "LiberationSans-Regular.ttf"));

    [Fact]
    public void Numbering_skips_attachments_and_continues_through_appendices()
    {
        var plan = InsertNumbering.Build(
            renderedPageCount: 7,
            slots:
            [
                new SlotHit("attachment-1", 2),
                new SlotHit("attachment-2", 4),
                new SlotHit("appendix-a", 6)
            ],
            inserts:
            [
                new InsertSpec("attachment-1", PdfInsertMode.Attachment, "Attachment 1", 2),
                new InsertSpec("attachment-2", PdfInsertMode.Attachment, "Attachment 2", 5),
                new InsertSpec("appendix-a", PdfInsertMode.Appendix, "Appendix A", 3)
            ]);

        Assert.Equal(14, plan.Pages.Count);
        Assert.Equal(7, plan.ReportTotal);
        Assert.Equal("5-7", plan.AppendixRanges["appendix-a"]);
        Assert.Equal(
            new[]
            {
                "report:1", "report:2",
                "attachment:1/2", "attachment:2/2",
                "report:3",
                "attachment:1/5", "attachment:2/5", "attachment:3/5", "attachment:4/5", "attachment:5/5",
                "report:4",
                "appendix:5", "appendix:6", "appendix:7"
            },
            plan.Pages.Select(Describe).ToArray());
        Assert.Equal("Attachment 2 - page 3 of 5", plan.Pages[7].AttachmentStamp);
        Assert.Equal("Page 7 of 7", plan.Pages[^1].ReportStamp);
        Assert.All(plan.Pages.Where(page => page.Kind == ComposedPageKind.Attachment), page => Assert.Null(page.ReportStamp));
    }

    [Fact]
    public void Numbering_requires_one_slot_per_insert()
    {
        var inserts = new[] { new InsertSpec("attachment-1", PdfInsertMode.Attachment, "Attachment 1", 2) };
        Assert.Throws<InvalidOperationException>(() => InsertNumbering.Build(3, [], inserts));
        Assert.Throws<InvalidOperationException>(() => InsertNumbering.Build(3, [new SlotHit("attachment-1", 0), new SlotHit("attachment-1", 1)], inserts));
        Assert.Throws<InvalidOperationException>(() => InsertNumbering.Build(3, [new SlotHit("attachment-1", 9)], inserts));
        Assert.Throws<InvalidOperationException>(() => InsertNumbering.Build(1, [new SlotHit("attachment-1", 0)], [new InsertSpec("attachment-1", PdfInsertMode.Attachment, "Attachment 1", 0)]));
    }

    [Fact]
    public void Report_only_numbering_counts_every_page()
    {
        var plan = InsertNumbering.ReportOnly(3);
        Assert.Equal(new[] { "Page 1 of 3", "Page 2 of 3", "Page 3 of 3" }, plan.Pages.Select(page => page.ReportStamp).ToArray());
    }

    [Fact]
    public void Inspector_rejects_corrupt_and_encrypted_pdfs_and_records_page_geometry()
    {
        var font = Font();
        var files = DemoInsertPdfs.Create(font);
        var ok = PdfInsertInspector.Inspect(files[DemoInsertPdfs.InstrumentLogFile]);
        var accepted = Assert.IsType<PdfInspection.Ok>(ok);
        Assert.Equal(5, accepted.PageCount);
        Assert.Equal(5, accepted.Pages.Count);
        Assert.False(accepted.Pages[0].Landscape);
        Assert.True(accepted.Pages[2].Landscape);
        Assert.Equal(64, SnapshotHasher.HashBytes(files[DemoInsertPdfs.InstrumentLogFile]).Length);

        var corrupt = PdfInsertInspector.Inspect([1, 2, 3, 4, 5]);
        var corruptRejected = Assert.IsType<PdfInspection.Rejected>(corrupt);
        Assert.Equal("insert.corrupt", corruptRejected.Code);

        var truncated = PdfInsertInspector.Inspect("%PDF-1.4\nthis is not a pdf"u8.ToArray());
        Assert.Equal("insert.corrupt", Assert.IsType<PdfInspection.Rejected>(truncated).Code);

        var secret = DemoInsertPdfs.Encrypted(font);
        var encryptedRejected = Assert.IsType<PdfInspection.Rejected>(PdfInsertInspector.Inspect(secret));
        Assert.Equal("insert.encrypted", encryptedRejected.Code);
        var secretHash = SnapshotHasher.HashBytes(secret);
        Assert.Contains(secretHash, PdfInsertInspector.Describe(encryptedRejected, secretHash), StringComparison.Ordinal);
    }

    [Fact]
    public void Template_rules_reject_a_bad_slot_and_keep_inserts_on_edit()
    {
        var template = Template("analytical-default") with
        {
            Inserts =
            [
                new PdfInsert { Id = "attachment-1", File = "../secret.pdf", Mode = PdfInsertMode.Attachment, Slot = "after:missing", Label = "Attachment 1" }
            ]
        };
        var issues = InsertRules.Validate(template);
        Assert.Contains(issues, issue => issue.Code == "insert.file");
        Assert.Contains(issues, issue => issue.Code == "insert.slot");

        var disabled = Template("analytical-default");
        disabled = disabled with
        {
            Sections = disabled.Sections.Select(section => section.InstanceId == "report-metadata" ? section with { Enabled = false } : section).ToList(),
            Inserts = [new PdfInsert { Id = "attachment-1", File = "chromatogram.pdf", Mode = PdfInsertMode.Attachment, Slot = "after:report-metadata", Label = "Attachment 1" }]
        };
        Assert.Contains(InsertRules.Validate(disabled), issue => issue.Code == "insert.disabled");

        var edited = TemplateEditor.Apply(Template("analytical-inserts"), new TemplateEdit
        {
            Name = "Renamed",
            SamplesPerPage = 2,
            ResultRowsPerChunk = 16,
            Paper = "A4",
            Orientation = "portrait",
            MarginTopMm = 24,
            MarginBottomMm = 16,
            MarginSideMm = 12,
            SignatoryId = "sig-demo-reviewer",
            Jurisdiction = "FICTION-ZONE-7",
            Sections = Template("analytical-inserts").Sections.Select(section => new SectionEdit
            {
                InstanceId = section.InstanceId,
                Title = section.Title,
                Enabled = section.Enabled,
                Columns = section.Columns,
                Subsections = section.Subsections
            }).ToList()
        });
        Assert.Equal(3, edited.Inserts.Count);
        Assert.Equal("attachment-2", edited.Inserts[1].Id);
    }

    [Fact]
    public async Task Merge_replaces_slot_pages_and_stamp_numbers_without_covering_insert_text()
    {
        var font = Font();
        ReportFonts.Ensure(font);
        var files = DemoInsertPdfs.Create(font);
        var rendered = SlotFixture.Rendered(font,
        [
            "REPORT-ALPHA\nAPXRANGE_APPENDIX_A",
            "INSERT-SLOT:attachment-1",
            "REPORT-BETA",
            "INSERT-SLOT:appendix-a"
        ]);
        var payloads = new[]
        {
            new InsertPayload("attachment-1", PdfInsertMode.Attachment, "Attachment 1", 2, files[DemoInsertPdfs.ChromatogramFile]),
            new InsertPayload("appendix-a", PdfInsertMode.Appendix, "Appendix A", 3, files[DemoInsertPdfs.AppendixFile])
        };
        var signer = new CaptureSigner();
        var signing = await ReportFinish.FinishAsync(rendered, payloads, font, signer, CancellationToken.None);
        Assert.NotNull(signer.Seen);
        Assert.Equal(signer.Seen, signing.Pdf);
        Assert.False(signing.CryptographicSignatureApplied);

        var pages = PdfTextLocator.PageTexts(signing.Pdf);
        Assert.Equal(7, pages.Count);
        Assert.DoesNotContain(pages, page => page.Contains("INSERT-SLOT:", StringComparison.Ordinal));
        Assert.Contains("REPORT-ALPHA", pages[0], StringComparison.Ordinal);
        Assert.Contains("Page 1 of 5", pages[0], StringComparison.Ordinal);
        Assert.Contains("3-5", pages[0], StringComparison.Ordinal);
        Assert.Contains("CHROMATOGRAM-A1", pages[1], StringComparison.Ordinal);
        Assert.Contains("Sheet 1 of 2", pages[1], StringComparison.Ordinal);
        Assert.Contains("Attachment 1 - page 1 of 2", pages[1], StringComparison.Ordinal);
        Assert.Contains("Trace baseline", pages[1], StringComparison.Ordinal);
        Assert.DoesNotMatch(new Regex(@"Page \d+ of \d+"), pages[1]);
        Assert.Contains("Attachment 1 - page 2 of 2", pages[2], StringComparison.Ordinal);
        Assert.Contains("Page 2 of 5", pages[3], StringComparison.Ordinal);
        Assert.Contains("REPORT-BETA", pages[3], StringComparison.Ordinal);
        Assert.Equal(new[] { "Page 3 of 5", "Page 4 of 5", "Page 5 of 5" }, new[] { pages[4], pages[5], pages[6] }.Select(page => Regex.Match(page, @"Page \d+ of \d+").Value).ToArray());
        Assert.Contains("APPENDIX-BODY-1", pages[4], StringComparison.Ordinal);
        Assert.Contains("APPENDIX-BODY-3", pages[6], StringComparison.Ordinal);

        var body = PdfTextLocator.Find(signing.Pdf, "Trace baseline").Single(hit => hit.PageIndex == 1);
        var label = PdfTextLocator.Find(signing.Pdf, "Attachment 1 - page 1 of 2").Single();
        Assert.False(body.Overlaps(label));
        Assert.True(label.Top <= body.Bottom, $"Label top {label.Top} should sit below the body baseline {body.Bottom}.");

        var sizes = PdfTextLocator.PageSizes(signing.Pdf);
        Assert.Equal(sizes[0].Height, sizes[3].Height, 1);
        Assert.True(sizes[1].Height > sizes[0].Height + 20);
        var log = files[DemoInsertPdfs.InstrumentLogFile];
        var logged = PdfInsertComposer.Merge(SlotFixture.Rendered(font, ["INSERT-SLOT:attachment-2"]), [new InsertPayload("attachment-2", PdfInsertMode.Attachment, "Attachment 2", 5, log)]);
        var stamped = PdfInsertComposer.Stamp(logged.Pdf, logged.Plan, font);
        var logSizes = PdfTextLocator.PageSizes(stamped);
        var landscape = logSizes.Single(size => size.Width > size.Height);
        Assert.True(landscape.Width > landscape.Height);
        Assert.Contains(PdfTextLocator.PageTexts(stamped), page => page.Contains("This page is landscape.", StringComparison.Ordinal) && page.Contains("Attachment 2 - page 3 of 5", StringComparison.Ordinal));
    }

    [Fact]
    public void Pinned_hash_changes_when_an_insert_digest_changes()
    {
        var template = Template("analytical-default");
        var pin = new PinnedInput
        {
            SchemaVersion = "1.0",
            Snapshot = Snapshot(),
            Template = template,
            Texts = [],
            Mrls = [],
            Signatory = null,
            SamplesPerPage = 2,
            Paper = "A4",
            Orientation = "portrait",
            Inserts = [new InsertDigest { Id = "attachment-1", File = "chromatogram.pdf", Sha256 = new string('a', 64), PageCount = 2, Accepted = true }]
        };
        var hash = SnapshotHasher.Hash(pin);
        var changed = pin with
        {
            Inserts = [new InsertDigest { Id = "attachment-1", File = "chromatogram.pdf", Sha256 = new string('b', 64), PageCount = 2, Accepted = true }]
        };
        Assert.NotEqual(hash, SnapshotHasher.Hash(changed));
    }

    private static string Describe(ComposedPage page) => page.Kind switch
    {
        ComposedPageKind.Report => "report:" + page.ReportNumber,
        ComposedPageKind.Attachment => "attachment:" + page.InsertPage + "/" + page.InsertPageCount,
        _ => "appendix:" + page.ReportNumber
    };

    private static TemplateDefinition Template(string id)
    {
        var path = Path.Combine(DeskFactory.FindContentRoot(), "Seed", "templates", id + ".json");
        return System.Text.Json.JsonSerializer.Deserialize<TemplateDefinition>(File.ReadAllText(path), LabReport.Core.JsonDefaults.Options)!;
    }

    private static ReportSnapshot Snapshot()
    {
        var path = Path.Combine(DeskFactory.FindContentRoot(), "Seed", "feeds", "report-snapshot-v1", "demo-tenant", "RPT-DEMO-0001.json");
        return System.Text.Json.JsonSerializer.Deserialize<ReportSnapshot>(File.ReadAllText(path), LabReport.Core.JsonDefaults.Options)!;
    }

    private sealed class CaptureSigner : IPostRenderSigner
    {
        public byte[]? Seen { get; private set; }

        public Task<SigningHookResult> AfterRenderAsync(byte[] pdf, CancellationToken cancellationToken)
        {
            Seen = pdf;
            return Task.FromResult(new SigningHookResult(pdf, false, "stub"));
        }
    }
}

[Collection("desk")]
public class InsertRenderTests
{
    private readonly DeskFactory _factory;

    public InsertRenderTests(DeskFactory factory) => _factory = factory;

    [Fact]
    public async Task Html_places_slot_pages_and_lists_page_counts()
    {
        var html = await RenderAsync();
        Assert.Contains("id=\"insert-list\"", html, StringComparison.Ordinal);
        Assert.Contains("data-slot=\"attachment-1\"", html, StringComparison.Ordinal);
        Assert.Contains("class=\"insert-slot\"", html, StringComparison.Ordinal);
        Assert.Contains("INSERT-SLOT:attachment-1", html, StringComparison.Ordinal);
        Assert.Contains("INSERT-SLOT:attachment-2", html, StringComparison.Ordinal);
        Assert.Contains("INSERT-SLOT:appendix-a", html, StringComparison.Ordinal);
        Assert.Contains("APXRANGE_APPENDIX_A", html, StringComparison.Ordinal);
        Assert.Contains("Not numbered", html, StringComparison.Ordinal);
        Assert.Contains(">2<", html, StringComparison.Ordinal);
        Assert.Contains(">5<", html, StringComparison.Ordinal);
        Assert.Contains(">3<", html, StringComparison.Ordinal);
        Assert.Contains("Mixed, landscape page 3", html, StringComparison.Ordinal);
        var attachment1 = html.IndexOf("data-slot=\"attachment-1\"", StringComparison.Ordinal);
        var group = html.IndexOf("Sample group 1 of", StringComparison.Ordinal);
        var methods = html.IndexOf(">Method references<", StringComparison.Ordinal);
        var attachment2 = html.IndexOf("data-slot=\"attachment-2\"", StringComparison.Ordinal);
        var disclaimer = html.IndexOf("not valid for trade or legal use", StringComparison.Ordinal);
        var appendix = html.IndexOf("data-slot=\"appendix-a\"", StringComparison.Ordinal);
        Assert.True(attachment1 > 0 && attachment1 < group);
        Assert.True(methods > 0 && methods < attachment2 && attachment2 < disclaimer && disclaimer < appendix);
        Assert.DoesNotContain("Html.Raw", File.ReadAllText(Path.Combine(DeskFactory.FindContentRoot(), "Views", "Report", "Sections", "_InsertSlot.cshtml")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Published_pdf_numbers_report_and_appendix_and_labels_attachments()
    {
        var authorizer = _factory.Services.GetRequiredService<DemoAuthorizer>();
        var feed = _factory.Services.GetRequiredService<IReportFeed>();
        var templates = _factory.Services.GetRequiredService<FileTemplateStore>();
        var catalogs = _factory.Services.GetRequiredService<ICatalogSource>();
        var preparation = _factory.Services.GetRequiredService<ReportPreparation>();
        var renderer = _factory.Services.GetRequiredService<ReportDocumentRenderer>();
        var fonts = _factory.Services.GetRequiredService<ReportFontAsset>();
        var signer = _factory.Services.GetRequiredService<IPostRenderSigner>();
        var snapshot = await feed.GetAsync(authorizer.TenantId, "RPT-DEMO-0001", authorizer.PrincipalId, CancellationToken.None);
        var template = templates.Require("analytical-inserts");
        var prepared = preparation.Prepare(snapshot!, template, catalogs.Texts(), catalogs.Mrls(), catalogs.Signatory(template.SignatoryId), false);
        Assert.True(prepared.Plan.IsValid, string.Join("; ", prepared.Plan.Issues.Select(issue => issue.Message)));
        Assert.Equal(3, prepared.Inspection.Digests.Count);
        Assert.All(prepared.Inspection.Digests, digest => Assert.True(digest.Accepted && digest.Sha256.Length == 64));
        var rendered = await renderer.RenderPdfAsync(prepared.Plan, CancellationToken.None);
        var signing = await ReportFinish.FinishAsync(rendered, preparation.PayloadsFor(prepared.Plan), fonts.Regular, signer, CancellationToken.None);
        var path = Path.Combine(_factory.Storage, "inserts.pdf");
        await File.WriteAllBytesAsync(path, signing.Pdf);
        var text = Run("pdftotext", "-layout \"" + path + "\" -");
        Assert.DoesNotContain("INSERT-SLOT:", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Placeholder page", text, StringComparison.Ordinal);
        Assert.Contains("CHROMATOGRAM-A1", text, StringComparison.Ordinal);
        Assert.Contains("Sheet 1 of 2", text, StringComparison.Ordinal);
        Assert.Contains("INSTRUMENT-LOG-3", text, StringComparison.Ordinal);
        Assert.Contains("This page is landscape.", text, StringComparison.Ordinal);
        Assert.Contains("APPENDIX-BODY-1", text, StringComparison.Ordinal);
        var pages = text.Split('\f');
        if (pages.Length > 0 && pages[^1].Length == 0)
        {
            pages = pages[..^1];
        }

        static bool IsAttachment(string page) =>
            page.Contains("CHROMATOGRAM-", StringComparison.Ordinal) || page.Contains("INSTRUMENT-LOG-", StringComparison.Ordinal);

        var expectedTotal = pages.Count(page => !IsAttachment(page));
        var reportNumbers = new List<int>();
        var attachmentLabels = new List<string>();
        foreach (var page in pages)
        {
            if (IsAttachment(page))
            {
                Assert.DoesNotMatch(@"Page \d+ of \d+", page);
                var label = Regex.Match(page, @"Attachment \d+ - page \d+ of \d+");
                Assert.True(label.Success, page);
                attachmentLabels.Add(label.Value);
                continue;
            }

            var report = Regex.Matches(page, @"Page (\d+) of (\d+)");
            Assert.Single(report);
            Assert.Equal(expectedTotal.ToString(System.Globalization.CultureInfo.InvariantCulture), report[0].Groups[2].Value);
            reportNumbers.Add(int.Parse(report[0].Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture));
            Assert.DoesNotMatch(@"Attachment \d+ - page", page);
        }

        Assert.Equal(Enumerable.Range(1, expectedTotal), reportNumbers);
        Assert.Equal(
            new[]
            {
                "Attachment 1 - page 1 of 2",
                "Attachment 1 - page 2 of 2",
                "Attachment 2 - page 1 of 5",
                "Attachment 2 - page 2 of 5",
                "Attachment 2 - page 3 of 5",
                "Attachment 2 - page 4 of 5",
                "Attachment 2 - page 5 of 5"
            },
            attachmentLabels);
        Assert.Contains($"{reportNumbers[^3]}-{reportNumbers[^1]}", text, StringComparison.Ordinal);
    }

    private async Task<string> RenderAsync()
    {
        var authorizer = _factory.Services.GetRequiredService<DemoAuthorizer>();
        var feed = _factory.Services.GetRequiredService<IReportFeed>();
        var templates = _factory.Services.GetRequiredService<FileTemplateStore>();
        var catalogs = _factory.Services.GetRequiredService<ICatalogSource>();
        var preparation = _factory.Services.GetRequiredService<ReportPreparation>();
        var snapshot = await feed.GetAsync(authorizer.TenantId, "RPT-DEMO-0001", authorizer.PrincipalId, CancellationToken.None);
        var template = templates.Require("analytical-inserts");
        var prepared = preparation.Prepare(snapshot!, template, catalogs.Texts(), catalogs.Mrls(), catalogs.Signatory(template.SignatoryId), false);
        Assert.True(prepared.Plan.IsValid, string.Join("; ", prepared.Plan.Issues.Select(issue => issue.Message)));
        return await _factory.Services.GetRequiredService<ReportDocumentRenderer>().RenderHtmlAsync(prepared.Plan, CancellationToken.None);
    }

    private static string Run(string file, string arguments)
    {
        var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(file, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true
        })!;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        Assert.Equal(0, process.ExitCode);
        return output;
    }
}
