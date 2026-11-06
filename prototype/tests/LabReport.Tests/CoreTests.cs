using System.Text.Json;
using LabReport.Core;
using LabReport.Core.Models;
using LabReport.Core.Rendering;
using LabReport.Core.Services;
using LabReport.Web.Services;

namespace LabReport.Tests;

public class CoreTests
{
    [Theory]
    [InlineData(1, 3)]
    [InlineData(2, 2)]
    [InlineData(3, 1)]
    public void Sample_groups_cover_every_sample_at_1_2_and_3(int perPage, int expectedGroups)
    {
        var snapshot = Load("RPT-DEMO-0001");
        var groups = SampleGrouper.Group(snapshot.Samples, perPage);
        Assert.Equal(expectedGroups, groups.Count);
        Assert.Equal(snapshot.Samples.Select(sample => sample.SampleId), groups.SelectMany(group => group.Samples).Select(sample => sample.SampleId));
        Assert.All(groups, group => Assert.InRange(group.Samples.Count, 1, perPage));
    }

    [Fact]
    public void Grouper_rejects_zero_and_does_not_drop_the_tail()
    {
        var snapshot = Load("RPT-DEMO-STRESS");
        Assert.Throws<ArgumentOutOfRangeException>(() => SampleGrouper.Group(snapshot.Samples, 0));
        var groups = SampleGrouper.Group(snapshot.Samples, 3);
        Assert.Equal(3, groups.Count);
        Assert.Equal(2, groups[^1].Samples.Count);
        Assert.Equal(8, groups.Sum(group => group.Samples.Count));
    }

    [Fact]
    public void Text_resolver_encodes_later_and_rejects_unknown_or_missing_placeholders()
    {
        var entry = Text("comment.oil-a", TextScope.Sample, TextCategory.Comment, "Demo comment bound to sample {{sampleId}}.");
        var (plain, error) = TextResolver.Resolve(entry, new Dictionary<string, string?> { ["sampleId"] = "DEMO-SMP-01" });
        Assert.Null(error);
        Assert.Equal("Demo comment bound to sample DEMO-SMP-01.", plain);
        Assert.Equal("A &lt;tag&gt;", TextResolver.Encode("A <tag>"));

        var unknown = TextResolver.Resolve(entry with { Body = "Hello {{signatoryName}}" }, new Dictionary<string, string?> { ["signatoryName"] = "R. Quinn" });
        Assert.Contains("not allowed", unknown.Error, StringComparison.Ordinal);

        var missing = TextResolver.Resolve(entry, new Dictionary<string, string?>());
        Assert.Contains("missing a value", missing.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Text_selection_skips_drafts_and_expired_versions()
    {
        var entries = Texts().Entries;
        var selected = TextResolver.Select(entries, "disclaimer.scope", "en", new DateOnly(2026, 4, 15));
        Assert.NotNull(selected);
        Assert.Equal("1.0.0", selected!.VersionLabel);
        Assert.DoesNotContain("DRAFT-DISCLAIMER", selected.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("OLD-DISCLAIMER-9", selected.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void Mrl_does_not_pass_or_fail_when_unit_or_limit_is_unusable()
    {
        var snapshot = Load("RPT-DEMO-0001");
        var limits = Mrls().Limits;
        var sample = snapshot.Samples[0];
        var rows = MrlEvaluator.Evaluate(snapshot, sample, "FICTION-ZONE-7", limits).ToDictionary(row => row.AnalyteCode);
        Assert.Equal(MrlStatus.WithinFictionalLimit, rows["DM-PB"].Status);
        Assert.True(rows["DM-PB"].IsNumericComparison);
        Assert.Equal(MrlStatus.BelowLoqNotExceedingLimit, rows["DM-AS"].Status);
        Assert.False(rows["DM-AS"].IsNumericComparison);
        Assert.Equal(MrlStatus.NotDetectedUncompared, rows["DM-CU"].Status);
        Assert.Equal(MrlStatus.NotTested, rows["DM-FE"].Status);
        Assert.Equal(MrlStatus.UnitMismatch, rows["DM-CD"].Status);
        Assert.False(rows["DM-CD"].IsNumericComparison);
        Assert.Equal(MrlStatus.NoApplicableLimit, rows["DM-ZN"].Status);
        Assert.Equal(MrlStatus.MissingValue, rows["DM-NI"].Status);

        var second = MrlEvaluator.Evaluate(snapshot, snapshot.Samples[1], "FICTION-ZONE-7", limits).ToDictionary(row => row.AnalyteCode);
        Assert.Equal(MrlStatus.ExceedsFictionalLimit, second["DM-PB"].Status);
        Assert.Equal(MrlStatus.InconclusiveLoqAboveLimit, second["DM-AS"].Status);
        Assert.Equal(MrlStatus.NoApplicableLimit, second["DM-TAG"].Status);
    }

    [Fact]
    public void Ambiguous_limits_are_not_compared()
    {
        var snapshot = Load("RPT-DEMO-0001");
        var sample = snapshot.Samples[0];
        var result = sample.Results.First(item => item.AnalyteCode == "DM-PB");
        var limits = new[]
        {
            Limit("a", result.AnalyteCode, new DateOnly(2026, 1, 1)),
            Limit("b", result.AnalyteCode, new DateOnly(2026, 1, 1))
        };
        var evaluation = MrlEvaluator.EvaluateOne(snapshot, sample, result, "FICTION-ZONE-7", limits);
        Assert.Equal(MrlStatus.AmbiguousLimit, evaluation.Status);
        Assert.False(evaluation.IsNumericComparison);
    }

    [Fact]
    public void Plan_isolates_sample_text_and_deduplicates_methods()
    {
        var plan = PlanDefault();
        var first = plan.Groups.SelectMany(group => group.Samples).Single(sample => sample.Sample.SampleId == "DEMO-SMP-01");
        var second = plan.Groups.SelectMany(group => group.Samples).Single(sample => sample.Sample.SampleId == "DEMO-SMP-02");
        var third = plan.Groups.SelectMany(group => group.Samples).Single(sample => sample.Sample.SampleId == "DEMO-SMP-03");
        Assert.Contains(first.Comments, text => text.PlainText.Contains("DEMO-SMP-01", StringComparison.Ordinal));
        Assert.DoesNotContain(first.Comments, text => text.PlainText.Contains("DEMO-SMP-02", StringComparison.Ordinal));
        Assert.Contains(second.Comments, text => text.PlainText.Contains("Second sample comment for DEMO-SMP-02", StringComparison.Ordinal));
        Assert.Empty(third.Comments);
        Assert.Contains(first.ResultNotes, note => note.ResultId == "RES-DEMO-SMP-01-PB" && note.Text.PlainText.Contains("DEMO-SMP-01", StringComparison.Ordinal));
        Assert.DoesNotContain(second.ResultNotes, note => note.Text.PlainText.Contains("Fictional lead analogue", StringComparison.Ordinal));
        Assert.Equal(3, plan.Methods.Count);
        Assert.Equal(1, plan.Methods.Count(method => method.Method.MethodCode == "DEMO-MET-01"));
        Assert.Equal("not valid for trade or legal use", plan.ReportDisclaimers.Single().PlainText.Split("not valid for trade or legal use")[1] == "" ? "not valid for trade or legal use" : "not valid for trade or legal use");
        Assert.Single(plan.ReportDisclaimers);
        Assert.Single(plan.ReportConclusions);
        Assert.Contains(first.Withheld, item => item.AnalyteCode == "DM-HELD");
        Assert.DoesNotContain(plan.PerSample, section => section.Type == "mrl-comparison");
        Assert.Equal(2, plan.Groups.Count);
    }

    [Fact]
    public void Unresolved_required_placeholder_fails_validation()
    {
        var snapshot = Load("RPT-DEMO-0001");
        var template = Template("analytical-default");
        var texts = Texts().Entries.Select(entry => entry.Key == "disclaimer.scope" && entry.VersionLabel == "1.0.0"
            ? entry with { Body = "Broken {{notARealToken}} for {{reportNumber}}." }
            : entry).ToList();
        var plan = RenderPlanner.Plan(snapshot, template, texts, Mrls().Limits, Signatory(), false);
        Assert.False(plan.IsValid);
        Assert.Contains(plan.Issues, issue => issue.Code == "text.placeholder");
    }

    [Fact]
    public void Raw_markup_in_catalog_text_fails_validation()
    {
        var snapshot = Load("RPT-DEMO-0001");
        var template = Template("analytical-default");
        var texts = Texts().Entries.Select(entry => entry.Key == "comment.oil-a"
            ? entry with { Body = "Hello <b>{{sampleId}}</b>" }
            : entry).ToList();
        var plan = RenderPlanner.Plan(snapshot, template, texts, Mrls().Limits, Signatory(), false);
        Assert.Contains(plan.Issues, issue => issue.Message.Contains("Raw markup", StringComparison.Ordinal) || issue.Message.Contains("raw markup", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Adding_the_mrl_section_is_config_only_and_unknown_types_are_rejected()
    {
        var template = Template("analytical-default");
        var added = TemplateEditor.AddSection(template, "mrl-comparison");
        Assert.Equal(template.Version + 1, added.Version);
        Assert.Contains(added.Sections, section => section.Type == "mrl-comparison" && section.Enabled);
        Assert.Throws<InvalidOperationException>(() => TemplateEditor.AddSection(added, "mrl-comparison"));
        Assert.Throws<InvalidOperationException>(() => TemplateEditor.AddSection(template, "custom-html"));
        var plan = RenderPlanner.Plan(Load("RPT-DEMO-0001"), added, Texts().Entries, Mrls().Limits, Signatory(), false);
        Assert.True(plan.IsValid);
        Assert.Contains(plan.PerSample, section => section.Type == "mrl-comparison");
    }

    [Fact]
    public void Pinned_hash_does_not_change_when_a_later_catalog_edit_is_separate()
    {
        var template = Template("analytical-default");
        var texts = Texts().Entries;
        var pin = new PinnedInput
        {
            SchemaVersion = "1.0",
            Snapshot = Load("RPT-DEMO-0001"),
            Template = template,
            Texts = texts.Where(entry => entry.ApprovalStatus == ApprovalStatus.Approved && entry.VersionLabel == "1.0.0").ToList(),
            Mrls = Mrls().Limits,
            Signatory = Signatory(),
            SamplesPerPage = 2,
            Paper = "A4",
            Orientation = "portrait"
        };
        var hash = SnapshotHasher.Hash(pin);
        var edited = pin with
        {
            Texts = pin.Texts.Select(entry => entry.Key == "disclaimer.scope" ? entry with { Body = "Changed {{reportNumber}}." } : entry).ToList()
        };
        Assert.Equal(hash, SnapshotHasher.Hash(pin));
        Assert.NotEqual(hash, SnapshotHasher.Hash(edited));
    }

    [Fact]
    public void Feed_names_and_paths_reject_sql_urls_and_traversal()
    {
        Assert.True(FeedContracts.IsSafeContractName(FeedContracts.ReportSnapshotV1));
        Assert.False(FeedContracts.IsSafeContractName("https://evil.example/feed"));
        Assert.False(FeedContracts.IsSafeContractName("report-snapshot-v1; DROP TABLE lab.Report"));
        Assert.True(FeedContracts.LooksHostile("SELECT * FROM lab.Report"));
        var root = Path.Combine(Path.GetTempPath(), "labreport-path-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Assert.Throws<InvalidOperationException>(() => PathSafety.CombineUnderRoot(root, "..", "etc", "passwd"));
        Assert.Throws<InvalidOperationException>(() => PathSafety.CombineUnderRoot(root, "/etc/passwd"));
        var safe = PathSafety.CombineUnderRoot(root, "artifacts", "abc.pdf");
        Assert.StartsWith(Path.GetFullPath(root), safe, StringComparison.Ordinal);
    }

    [Fact]
    public void Json_feed_orders_results_under_their_sample()
    {
        var snapshot = Load("RPT-DEMO-0001");
        Assert.Equal("1.0", snapshot.SchemaVersion);
        Assert.Equal(new[] { "DEMO-SMP-01", "DEMO-SMP-02", "DEMO-SMP-03" }, snapshot.Samples.Select(sample => sample.SampleId));
        foreach (var sample in snapshot.Samples)
        {
            Assert.All(sample.Results, result => Assert.Equal(sample.SampleId, result.SampleId));
            Assert.Equal(sample.Results.OrderBy(result => result.Ordinal).Select(result => result.ResultId), sample.Results.Select(result => result.ResultId));
        }

        Assert.Contains("{{reportNumber}}", snapshot.Samples[2].Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Sql_scripts_are_parameterized_and_keep_a_second_tenant_out_of_the_reader_contract()
    {
        var root = DeskFactory.FindRepoRoot();
        var schema = File.ReadAllText(Path.Combine(root, "sql", "001_schema.sql"));
        var procs = File.ReadAllText(Path.Combine(root, "sql", "003_views_procs.sql"));
        var seed = File.ReadAllText(Path.Combine(root, "sql", "002_seed.sql"));
        var login = File.ReadAllText(Path.Combine(root, "sql", "004_login.sql"));
        Assert.Contains("CREATE OR ALTER PROCEDURE rpt.usp_ExportReportSnapshot", procs, StringComparison.Ordinal);
        Assert.Contains("@TenantId", procs, StringComparison.Ordinal);
        Assert.Contains("@ReportId", procs, StringComparison.Ordinal);
        Assert.Contains("@PrincipalId", procs, StringComparison.Ordinal);
        Assert.Contains("lab.ReportAccess", procs, StringComparison.Ordinal);
        Assert.DoesNotContain("sp_executesql", procs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EXEC (", procs, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("rpt.vw_ResultGrain", procs, StringComparison.Ordinal);
        Assert.Contains("CREATE ROLE rpt_reader", procs, StringComparison.Ordinal);
        Assert.Contains("RPT-DEMO-0001", seed, StringComparison.Ordinal);
        Assert.Contains("RPT-OTHER-0001", seed, StringComparison.Ordinal);
        Assert.Contains("demo-reader", seed, StringComparison.Ordinal);
        Assert.Contains("other-reader", seed, StringComparison.Ordinal);
        Assert.Contains("Fictional tag <demo>", seed, StringComparison.Ordinal);
        Assert.Contains("CK_Mrl_Fictional", schema, StringComparison.Ordinal);
        Assert.Contains("$(AppPassword)", login, StringComparison.Ordinal);
        Assert.DoesNotContain("Password=Change", login, StringComparison.Ordinal);
        Assert.Equal("rpt.usp_ExportReportSnapshot", SqlReportFeed.SnapshotCommand);
    }

    private static RenderPlan PlanDefault()
    {
        return RenderPlanner.Plan(Load("RPT-DEMO-0001"), Template("analytical-default"), Texts().Entries, Mrls().Limits, Signatory(), false);
    }

    private static ReportSnapshot Load(string reportId)
    {
        var path = Path.Combine(DeskFactory.FindContentRoot(), "Seed", "feeds", "report-snapshot-v1", "demo-tenant", reportId + ".json");
        var snapshot = JsonSerializer.Deserialize<ReportSnapshot>(File.ReadAllText(path), JsonDefaults.Options)!;
        return SnapshotCanonicalizer.Canonicalize(snapshot);
    }

    private static TemplateDefinition Template(string id)
    {
        var path = Path.Combine(DeskFactory.FindContentRoot(), "Seed", "templates", id + ".json");
        return JsonSerializer.Deserialize<TemplateDefinition>(File.ReadAllText(path), JsonDefaults.Options)!;
    }

    private static TextCatalogFile Texts()
    {
        var path = Path.Combine(DeskFactory.FindContentRoot(), "Seed", "catalogs", "texts.json");
        return JsonSerializer.Deserialize<TextCatalogFile>(File.ReadAllText(path), JsonDefaults.Options)!;
    }

    private static MrlCatalogFile Mrls()
    {
        var path = Path.Combine(DeskFactory.FindContentRoot(), "Seed", "catalogs", "mrl.json");
        return JsonSerializer.Deserialize<MrlCatalogFile>(File.ReadAllText(path), JsonDefaults.Options)!;
    }

    private static SignatoryBlock Signatory()
    {
        var path = Path.Combine(DeskFactory.FindContentRoot(), "Seed", "catalogs", "signatories.json");
        return JsonSerializer.Deserialize<SignatoryCatalogFile>(File.ReadAllText(path), JsonDefaults.Options)!.Signatories[0];
    }

    private static TextEntry Text(string key, TextScope scope, TextCategory category, string body) => new()
    {
        Key = key,
        Language = "en",
        VersionLabel = "1.0.0",
        Scope = scope,
        Category = category,
        EffectiveFrom = new DateOnly(2026, 1, 1),
        ApprovalStatus = ApprovalStatus.Approved,
        Body = body
    };

    private static MrlLimit Limit(string id, string analyte, DateOnly from) => new()
    {
        MrlId = id,
        AnalyteCode = analyte,
        Jurisdiction = "FICTION-ZONE-7",
        Matrix = "oil",
        Commodity = "refined-demo-oil",
        Unit = "mg/kg",
        LimitValue = 1.11m,
        EffectiveFrom = from,
        SourceName = "Fictional Demo Residue Catalog",
        VersionLabel = "fic-2026.1",
        Fictional = true
    };
}
