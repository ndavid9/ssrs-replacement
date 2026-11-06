using System.Diagnostics;
using LabReport.Core.Rendering;
using Microsoft.Extensions.DependencyInjection;
using LabReport.Core.Services;
using LabReport.Web.Services;

namespace LabReport.Tests;

[Collection("desk")]
public class RenderTests
{
    private readonly DeskFactory _factory;

    public RenderTests(DeskFactory factory) => _factory = factory;

    [Fact]
    public async Task Html_encodes_names_keeps_literal_braces_and_places_notes_on_the_owning_sample()
    {
        var html = await RenderAsync("RPT-DEMO-0001", "analytical-default", 2);
        Assert.Contains("Fictional tag &lt;demo&gt;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<demo>", html, StringComparison.Ordinal);
        Assert.Contains("The characters {{reportNumber}} are ordinary sample text.", html, StringComparison.Ordinal);
        Assert.Contains("not valid for trade or legal use", html, StringComparison.Ordinal);
        Assert.Equal(1, Count(html, "not valid for trade or legal use"));
        Assert.DoesNotContain("OLD-DISCLAIMER-9", html, StringComparison.Ordinal);
        Assert.DoesNotContain("DRAFT-DISCLAIMER", html, StringComparison.Ordinal);
        var sample1 = Slice(html, "sample-DEMO-SMP-01", "sample-DEMO-SMP-02");
        var sample2 = Slice(html, "sample-DEMO-SMP-02", "sample-DEMO-SMP-03");
        Assert.Contains("Demo comment bound to sample DEMO-SMP-01", sample1, StringComparison.Ordinal);
        Assert.DoesNotContain("Second sample comment for DEMO-SMP-02", sample1, StringComparison.Ordinal);
        Assert.Contains("Result note for Fictional lead analogue on sample DEMO-SMP-01", sample1, StringComparison.Ordinal);
        Assert.DoesNotContain("Result note for Fictional lead analogue", sample2, StringComparison.Ordinal);
        Assert.Contains("Second sample comment for DEMO-SMP-02 only", sample2, StringComparison.Ordinal);
        Assert.Equal(1, Count(html, "<strong>DEMO-MET-01</strong>"));
        Assert.Equal(1, Count(html, "Method note for DEMO-MET-01"));
        Assert.Contains("Sample group 1 of 2", html, StringComparison.Ordinal);
        Assert.Contains("Sample group 2 of 2", html, StringComparison.Ordinal);
        Assert.Contains("DM-HELD", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Fictional limit comparison", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Html.Raw", File.ReadAllText(Path.Combine(DeskFactory.FindContentRoot(), "Views", "Report", "AnalyticalReport.cshtml")), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1, 3)]
    [InlineData(2, 2)]
    [InlineData(3, 1)]
    public async Task Html_group_count_follows_samples_per_page(int perPage, int groups)
    {
        var html = await RenderAsync("RPT-DEMO-0001", "analytical-default", perPage);
        for (var number = 1; number <= groups; number++)
        {
            Assert.Contains($"Sample group {number} of {groups}", html, StringComparison.Ordinal);
        }

        Assert.DoesNotContain($"Sample group {groups + 1} of", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Mrl_section_appears_only_after_it_is_saved_on_the_template()
    {
        var before = await RenderAsync("RPT-DEMO-0001", "analytical-default", 2);
        Assert.DoesNotContain("fictional limits", before, StringComparison.OrdinalIgnoreCase);
        var templates = _factory.Services.GetRequiredService<FileTemplateStore>();
        var added = TemplateEditor.AddSection(templates.Require("analytical-default"), "mrl-comparison") with
        {
            TemplateId = "analytical-with-mrl",
            Name = "Analytical demonstration with fictional limits"
        };
        templates.Save(added);
        var reloaded = templates.Require("analytical-with-mrl");
        Assert.Equal(2, reloaded.Version);
        Assert.Contains(reloaded.Sections, section => section.Type == "mrl-comparison");
        var history = Path.Combine(_factory.Storage, "templates", "history", "analytical-with-mrl", "v2.json");
        Assert.True(File.Exists(history));
        var after = await RenderAsync("RPT-DEMO-0001", "analytical-with-mrl", 2);
        Assert.Contains("Fictional limit comparison", after, StringComparison.Ordinal);
        Assert.Contains("Numeric value is within the fictional limit.", after, StringComparison.Ordinal);
        Assert.Contains("Units differ and this demo does not convert them.", after, StringComparison.Ordinal);
        Assert.Contains("DEMO-SMP-01", Slice(after, "sample-DEMO-SMP-01", "sample-DEMO-SMP-02"), StringComparison.Ordinal);
        var original = templates.Require("analytical-default");
        Assert.DoesNotContain(original.Sections, section => section.Type == "mrl-comparison");
    }

    [Fact]
    public async Task Playwright_writes_real_pdfs_for_the_normal_mrl_and_stress_reports()
    {
        var directory = Path.Combine(_factory.Storage, "pdfs");
        Directory.CreateDirectory(directory);
        var normal = await PdfAsync("RPT-DEMO-0001", "analytical-default", 2, Path.Combine(directory, "normal.pdf"));
        var templates = _factory.Services.GetRequiredService<FileTemplateStore>();
        if (templates.Find("analytical-with-mrl") is null)
        {
            templates.Save(TemplateEditor.AddSection(templates.Require("analytical-default"), "mrl-comparison") with
            {
                TemplateId = "analytical-with-mrl",
                Name = "Analytical demonstration with fictional limits"
            });
        }

        var withMrl = await PdfAsync("RPT-DEMO-0001", "analytical-with-mrl", 2, Path.Combine(directory, "mrl.pdf"));
        var stress = await PdfAsync("RPT-DEMO-STRESS", "analytical-stress", 2, Path.Combine(directory, "stress.pdf"));
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(normal.Bytes[..5]), StringComparison.Ordinal);
        Assert.Contains("DEMO-SMP-01", normal.Text, StringComparison.Ordinal);
        Assert.Contains("DEMO-SMP-02", normal.Text, StringComparison.Ordinal);
        Assert.Contains("DEMO-SMP-03", normal.Text, StringComparison.Ordinal);
        Assert.Contains("NOT A VALID LABORATORY CERTIFICATE", normal.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Fictional limit comparison", normal.Text, StringComparison.Ordinal);
        Assert.Contains("Fictional limit comparison", withMrl.Text, StringComparison.Ordinal);
        var flatMrl = System.Text.RegularExpressions.Regex.Replace(withMrl.Text, @"\s+", " ");
        Assert.Contains("Numeric value is within the fictional limit.", flatMrl, StringComparison.Ordinal);
        Assert.True(withMrl.Pages >= 1);
        for (var index = 1; index <= 8; index++)
        {
            Assert.Contains($"DEMO-STR-{index:00}", stress.Text, StringComparison.Ordinal);
        }

        for (var index = 1; index <= 30; index++)
        {
            Assert.Contains($"DM-S{index:00}", stress.Text, StringComparison.Ordinal);
        }

        Assert.Contains("Sample group 1 of 4", stress.Text, StringComparison.Ordinal);
        Assert.Contains("Sample group 4 of 4", stress.Text, StringComparison.Ordinal);
        Assert.Contains("continued", stress.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("No comment is on file for sample DEMO-STR-03", stress.Text, StringComparison.Ordinal);
        Assert.True(stress.Pages >= 3, $"Stress PDF had {stress.Pages} pages.");
        Assert.DoesNotContain("\f\f", stress.Text, StringComparison.Ordinal);
    }

    private async Task<string> RenderAsync(string reportId, string templateId, int samplesPerPage)
    {
        var plan = await BuildPlanAsync(reportId, templateId, samplesPerPage);
        Assert.True(plan.IsValid, string.Join("; ", plan.Issues.Select(issue => issue.Message)));
        return await _factory.Services.GetRequiredService<ReportDocumentRenderer>().RenderHtmlAsync(plan, CancellationToken.None);
    }

    private async Task<(byte[] Bytes, string Text, int Pages)> PdfAsync(string reportId, string templateId, int samplesPerPage, string path)
    {
        var plan = await BuildPlanAsync(reportId, templateId, samplesPerPage);
        var bytes = await _factory.Services.GetRequiredService<ReportDocumentRenderer>().RenderPdfAsync(plan, CancellationToken.None);
        await File.WriteAllBytesAsync(path, bytes);
        var text = Run("pdftotext", $"\"{path}\" -");
        var info = Run("pdfinfo", $"\"{path}\"");
        var pages = int.Parse(info.Split('\n').First(line => line.StartsWith("Pages:", StringComparison.Ordinal)).Split(':')[1].Trim(), System.Globalization.CultureInfo.InvariantCulture);
        return (bytes, text, pages);
    }

    private async Task<RenderPlan> BuildPlanAsync(string reportId, string templateId, int samplesPerPage)
    {
        var authorizer = _factory.Services.GetRequiredService<DemoAuthorizer>();
        var feed = _factory.Services.GetRequiredService<IReportFeed>();
        var templates = _factory.Services.GetRequiredService<FileTemplateStore>();
        var catalogs = _factory.Services.GetRequiredService<ICatalogSource>();
        var snapshot = await feed.GetAsync(authorizer.TenantId, reportId, authorizer.PrincipalId, CancellationToken.None);
        Assert.NotNull(snapshot);
        var template = templates.Require(templateId) with { SamplesPerPage = samplesPerPage };
        return RenderPlanner.Plan(snapshot!, template, catalogs.Texts(), catalogs.Mrls(), catalogs.Signatory(template.SignatoryId), false);
    }

    private static string Slice(string html, string startId, string endId)
    {
        var start = html.IndexOf("id=\"" + startId + "\"", StringComparison.Ordinal);
        var end = html.IndexOf("id=\"" + endId + "\"", StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        return html[start..end];
    }

    private static int Count(string haystack, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }

    private static string Run(string file, string arguments)
    {
        var process = Process.Start(new ProcessStartInfo(file, arguments)
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

[CollectionDefinition("desk")]
public sealed class DeskCollection : ICollectionFixture<DeskFactory>;
