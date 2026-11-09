using LabReport.Core.Models;
using LabReport.Core.Services;

namespace LabReport.Core.Rendering;

public sealed record RenderPlan
{
    public required ReportSnapshot Snapshot { get; init; }
    public required TemplateDefinition Template { get; init; }
    public required bool IsPreview { get; init; }
    public required IReadOnlyList<SampleGroupView> Groups { get; init; }
    public required IReadOnlyList<SectionConfig> BeforeSamples { get; init; }
    public required IReadOnlyList<SectionConfig> PerSample { get; init; }
    public required IReadOnlyList<SectionConfig> AfterSamples { get; init; }
    public required IReadOnlyList<MethodFootnote> Methods { get; init; }
    public required IReadOnlyList<ResolvedText> ReportDisclaimers { get; init; }
    public required IReadOnlyList<ResolvedText> ReportConclusions { get; init; }
    public required IReadOnlyDictionary<string, IReadOnlyList<ResolvedText>> SectionIntros { get; init; }
    public SignatoryBlock? Signatory { get; init; }
    public string? SignatoryDataUri { get; init; }
    public required IReadOnlyList<ValidationIssue> Issues { get; init; }
    public bool IsValid => Issues.Count == 0;
    public bool ResultsSectionEnabled { get; init; }
    public IReadOnlyList<ReportInsert> Inserts
    {
        get => _inserts;
        init => _inserts = value ?? [];
    }

    private readonly IReadOnlyList<ReportInsert> _inserts = [];
}

public sealed record SampleGroupView(int Number, int Count, IReadOnlyList<SampleView> Samples);

public sealed record SampleView
{
    public required SampleSnapshot Sample { get; init; }
    public required IReadOnlyList<ResultSubsectionView> Subsections { get; init; }
    public required IReadOnlyList<WithheldResult> Withheld { get; init; }
    public required IReadOnlyList<MrlEvaluation> MrlRows { get; init; }
    public required IReadOnlyList<ResolvedText> Comments { get; init; }
    public required IReadOnlyList<ResolvedText> Conclusions { get; init; }
    public required IReadOnlyList<ResultNoteView> ResultNotes { get; init; }
    public required IReadOnlyDictionary<string, IReadOnlyList<string>> NoteMarkersByResult { get; init; }
}

public sealed record ResultNoteView(string ResultId, string Marker, ResolvedText Text);

public sealed record ResultSubsectionView(string Id, string Title, IReadOnlyList<ResultChunkView> Chunks);

public sealed record ResultChunkView(int Number, int Count, bool Continued, IReadOnlyList<ResultSnapshot> Rows);

public sealed record WithheldResult(string SampleId, string AnalyteCode, string Reason);

public sealed record MethodFootnote(int Number, MethodReference Method, IReadOnlyList<ResolvedText> Notes);

public static class RenderPlanner
{
    private static readonly HashSet<string> SampleTypes = new(StringComparer.Ordinal)
    {
        "sample-summary", "analytical-results", "mrl-comparison", "comments", "conclusions"
    };

    public static RenderPlan Plan(
        ReportSnapshot snapshot,
        TemplateDefinition template,
        IReadOnlyList<TextEntry> texts,
        IReadOnlyList<MrlLimit> mrls,
        SignatoryBlock? signatory,
        bool isPreview,
        IReadOnlyList<ReportInsert>? inserts = null,
        IReadOnlyList<ValidationIssue>? insertFileIssues = null)
    {
        var issues = new List<ValidationIssue>();
        var canonical = SnapshotCanonicalizer.Canonicalize(snapshot);
        issues.AddRange(SnapshotValidator.Validate(canonical));
        issues.AddRange(MrlCatalogValidator.Validate(mrls));

        foreach (var section in template.Sections)
        {
            if (!SectionCatalog.IsKnown(section.Type))
            {
                issues.Add(new("section.unknown", $"Section type '{section.Type}' is not in the handler whitelist."));
            }
        }

        if (template.SamplesPerPage is < 1 or > SampleGrouper.MaxSamplesPerPage)
        {
            issues.Add(new("template.pagination", "Samples per page is outside 1 to 20."));
        }

        var insertStructure = InsertRules.Validate(template);
        issues.AddRange(insertStructure);
        if (insertFileIssues is not null)
        {
            issues.AddRange(insertFileIssues);
        }

        IReadOnlyList<ReportInsert> placedInserts = [];
        if (insertStructure.Count == 0 && template.Inserts.Count > 0 && (insertFileIssues is null || insertFileIssues.Count == 0))
        {
            if (inserts is null)
            {
                issues.Add(new("insert.files", "Insert files were not inspected."));
            }
            else
            {
                var match = InsertRules.Match(template.Inserts, inserts);
                issues.AddRange(match);
                if (match.Count == 0)
                {
                    placedInserts = inserts;
                }
            }
        }

        var samplesPerPage = Math.Clamp(template.SamplesPerPage, 1, SampleGrouper.MaxSamplesPerPage);
        var chunkSize = Math.Clamp(template.ResultRowsPerChunk, 8, 40);
        var enabled = template.Sections.Where(section => section.Enabled && SectionCatalog.IsKnown(section.Type))
            .OrderBy(section => section.Order)
            .ThenBy(section => section.InstanceId, StringComparer.Ordinal)
            .ToList();
        var perSample = enabled.Where(section => SampleTypes.Contains(section.Type)).ToList();
        var reportSections = enabled.Where(section => !SampleTypes.Contains(section.Type)).ToList();
        var pivot = perSample.Count == 0 ? int.MaxValue : perSample.Min(section => section.Order);
        var before = reportSections.Where(section => section.Order <= pivot).ToList();
        var after = reportSections.Where(section => section.Order > pivot).ToList();
        var resultsSection = perSample.FirstOrDefault(section => section.Type == "analytical-results");
        var approval = enabled.FirstOrDefault(section => section.Type == "approval-signature");
        if (approval is not null && signatory is null)
        {
            issues.Add(new("signatory.missing", "The approval section is enabled but the selected signatory id was not found."));
        }

        var selectedTexts = new List<ResolvedText>();
        var reportBag = ReportBag(canonical);
        foreach (var section in enabled)
        {
            foreach (var key in section.TextKeys.Distinct(StringComparer.Ordinal))
            {
                var entry = TextResolver.Select(texts, key, template.Language, canonical.ReportDate);
                if (entry is null)
                {
                    issues.Add(new("text.missing", $"Required text '{key}' has no approved version effective on {canonical.ReportDate:yyyy-MM-dd}."));
                    continue;
                }

                if (entry.Scope is TextScope.Sample or TextScope.Result or TextScope.Method)
                {
                    issues.Add(new("text.scope", $"Text '{key}' is {entry.Scope}-scoped and must be attached with a binding, not a section key."));
                    continue;
                }

                var bag = entry.Scope == TextScope.Section ? SectionBag(canonical, section, signatory) : reportBag;
                var (plain, error) = TextResolver.Resolve(entry, bag);
                if (error is not null || plain is null)
                {
                    issues.Add(new("text.placeholder", error ?? $"Text '{key}' did not resolve."));
                    continue;
                }

                selectedTexts.Add(new ResolvedText
                {
                    Key = entry.Key,
                    VersionLabel = entry.VersionLabel,
                    Scope = entry.Scope,
                    Category = entry.Category,
                    SectionInstanceId = section.InstanceId,
                    PlainText = plain
                });
            }
        }

        var methodsByCode = canonical.Methods.ToDictionary(method => method.MethodCode, StringComparer.Ordinal);
        foreach (var binding in canonical.TextBindings)
        {
            var entry = TextResolver.Select(texts, binding.TextKey, template.Language, canonical.ReportDate);
            if (entry is null)
            {
                issues.Add(new("text.binding", $"Binding '{binding.BindingId}' points at '{binding.TextKey}', which has no approved effective text."));
                continue;
            }

            if (entry.Scope != binding.Scope)
            {
                issues.Add(new("text.binding-scope", $"Binding '{binding.BindingId}' scope does not match text '{entry.Key}'."));
                continue;
            }

            var bag = BagForBinding(canonical, binding, methodsByCode, out var bagError);
            if (bagError is not null || bag is null)
            {
                issues.Add(new("text.binding-target", bagError ?? $"Binding '{binding.BindingId}' has no target."));
                continue;
            }

            var (plain, error) = TextResolver.Resolve(entry, bag);
            if (error is not null || plain is null)
            {
                issues.Add(new("text.placeholder", error ?? $"Binding '{binding.BindingId}' did not resolve."));
                continue;
            }

            selectedTexts.Add(new ResolvedText
            {
                Key = entry.Key,
                VersionLabel = entry.VersionLabel,
                Scope = entry.Scope,
                Category = entry.Category,
                SampleId = binding.SampleId,
                ResultId = binding.ResultId,
                MethodCode = binding.MethodCode,
                PlainText = plain
            });
        }

        var footnotes = new List<MethodFootnote>();
        var methodNumber = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var sample in canonical.Samples)
        {
            foreach (var result in sample.Results)
            {
                if (result.MethodCode is null || methodNumber.ContainsKey(result.MethodCode))
                {
                    continue;
                }

                if (!methodsByCode.TryGetValue(result.MethodCode, out var method))
                {
                    continue;
                }

                var number = methodNumber.Count + 1;
                methodNumber[result.MethodCode] = number;
                var notes = selectedTexts.Where(text => text.Scope == TextScope.Method && text.MethodCode == result.MethodCode).ToList();
                footnotes.Add(new MethodFootnote(number, method, notes));
            }
        }

        var markerSequence = 1;
        var markers = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var notesBySample = new Dictionary<string, List<ResultNoteView>>(StringComparer.Ordinal);
        foreach (var sample in canonical.Samples)
        {
            var sampleNotes = new List<ResultNoteView>();
            foreach (var result in sample.Results)
            {
                var notes = selectedTexts.Where(text => text.Scope == TextScope.Result && text.ResultId == result.ResultId)
                    .OrderBy(text => text.Key, StringComparer.Ordinal)
                    .ToList();
                foreach (var note in notes)
                {
                    var marker = "N" + markerSequence.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    markerSequence++;
                    if (!markers.TryGetValue(result.ResultId, out var list))
                    {
                        list = [];
                        markers[result.ResultId] = list;
                    }

                    list.Add(marker);
                    sampleNotes.Add(new ResultNoteView(result.ResultId, marker, note));
                }
            }

            notesBySample[sample.SampleId] = sampleNotes;
        }

        var groups = SampleGrouper.Group(canonical.Samples, samplesPerPage);
        var sampleViews = new List<SampleView>();
        foreach (var sample in canonical.Samples)
        {
            var withheld = new List<WithheldResult>();
            var subsections = new List<ResultSubsectionView>();
            if (resultsSection is null)
            {
                withheld.AddRange(sample.Results.Select(result => new WithheldResult(sample.SampleId, result.AnalyteCode, "Analytical results section is disabled.")));
            }
            else
            {
                var claimed = new HashSet<string>(StringComparer.Ordinal);
                foreach (var subsection in resultsSection.Subsections.OrderBy(item => item.Order))
                {
                    var rows = sample.Results.Where(result => string.Equals(result.AnalyteGroup, subsection.AnalyteGroup, StringComparison.Ordinal)).ToList();
                    foreach (var row in rows)
                    {
                        claimed.Add(row.ResultId);
                    }

                    if (!subsection.Enabled)
                    {
                        withheld.AddRange(rows.Select(row => new WithheldResult(sample.SampleId, row.AnalyteCode, $"Subsection '{subsection.Title}' is disabled.")));
                        continue;
                    }

                    subsections.Add(new ResultSubsectionView(subsection.Id, subsection.Title, Chunk(rows, chunkSize)));
                }

                foreach (var row in sample.Results.Where(result => !claimed.Contains(result.ResultId)))
                {
                    withheld.Add(new WithheldResult(sample.SampleId, row.AnalyteCode, "No enabled subsection lists this analyte group."));
                }
            }

            sampleViews.Add(new SampleView
            {
                Sample = sample,
                Subsections = subsections,
                Withheld = withheld,
                MrlRows = MrlEvaluator.Evaluate(canonical, sample, template.Jurisdiction, mrls),
                Comments = selectedTexts.Where(text => text.Scope == TextScope.Sample && text.Category == TextCategory.Comment && text.SampleId == sample.SampleId).ToList(),
                Conclusions = selectedTexts.Where(text => text.Scope == TextScope.Sample && text.Category == TextCategory.Conclusion && text.SampleId == sample.SampleId).ToList(),
                ResultNotes = notesBySample[sample.SampleId],
                NoteMarkersByResult = markers.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<string>)pair.Value, StringComparer.Ordinal)
            });
        }

        var viewsById = sampleViews.ToDictionary(view => view.Sample.SampleId, StringComparer.Ordinal);
        var groupViews = groups.Select(group => new SampleGroupView(
            group.Number,
            groups.Count,
            group.Samples.Select(sample => viewsById[sample.SampleId]).ToList())).ToList();

        var intros = selectedTexts.Where(text => text.SectionInstanceId is not null)
            .GroupBy(text => text.SectionInstanceId!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<ResolvedText>)group.ToList(), StringComparer.Ordinal);

        return new RenderPlan
        {
            Snapshot = canonical,
            Template = template with { SamplesPerPage = samplesPerPage, ResultRowsPerChunk = chunkSize },
            IsPreview = isPreview,
            Groups = groupViews,
            BeforeSamples = before,
            PerSample = perSample,
            AfterSamples = after,
            Methods = footnotes,
            ReportDisclaimers = selectedTexts.Where(text => text.Scope == TextScope.Report && text.Category == TextCategory.Disclaimer).DistinctBy(text => text.Key + text.VersionLabel).ToList(),
            ReportConclusions = selectedTexts.Where(text => text.Scope == TextScope.Report && text.Category == TextCategory.Conclusion).DistinctBy(text => text.Key + text.VersionLabel).ToList(),
            SectionIntros = intros,
            Signatory = signatory,
            Issues = issues,
            ResultsSectionEnabled = resultsSection is not null,
            Inserts = placedInserts
        };
    }

    public static IReadOnlyList<TextEntry> TextsUsed(RenderPlan plan, IReadOnlyList<TextEntry> catalog, string language)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var section in plan.Template.Sections.Where(section => section.Enabled))
        {
            foreach (var key in section.TextKeys)
            {
                keys.Add(key);
            }
        }

        foreach (var binding in plan.Snapshot.TextBindings)
        {
            keys.Add(binding.TextKey);
        }

        return keys
            .Select(key => TextResolver.Select(catalog, key, language, plan.Snapshot.ReportDate))
            .Where(entry => entry is not null)
            .Select(entry => entry!)
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .ThenBy(entry => entry.VersionLabel, StringComparer.Ordinal)
            .ToList();
    }

    private static List<ResultChunkView> Chunk(IReadOnlyList<ResultSnapshot> rows, int size)
    {
        if (rows.Count == 0)
        {
            return [];
        }

        var chunks = new List<ResultChunkView>();
        for (var index = 0; index < rows.Count; index += size)
        {
            var slice = rows.Skip(index).Take(size).ToList();
            chunks.Add(new ResultChunkView(chunks.Count + 1, (int)Math.Ceiling(rows.Count / (double)size), chunks.Count > 0, slice));
        }

        var count = chunks.Count;
        return chunks.Select(chunk => chunk with { Count = count }).ToList();
    }

    private static Dictionary<string, string?> ReportBag(ReportSnapshot snapshot) => new()
    {
        ["reportNumber"] = snapshot.ReportNumber,
        ["reportDate"] = snapshot.ReportDate.ToString("yyyy-MM-dd"),
        ["sampleCount"] = snapshot.Samples.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["clientName"] = snapshot.ClientName,
        ["matrix"] = snapshot.Matrix,
        ["commodity"] = snapshot.Commodity,
        ["title"] = snapshot.Title
    };

    private static Dictionary<string, string?> SectionBag(ReportSnapshot snapshot, SectionConfig section, SignatoryBlock? signatory) => new()
    {
        ["sectionTitle"] = section.Title,
        ["reportNumber"] = snapshot.ReportNumber,
        ["signatoryName"] = signatory?.DisplayName,
        ["signatoryRole"] = signatory?.RoleTitle,
        ["signatoryStatement"] = signatory?.Statement
    };

    private static Dictionary<string, string?>? BagForBinding(
        ReportSnapshot snapshot,
        TextBinding binding,
        IReadOnlyDictionary<string, MethodReference> methods,
        out string? error)
    {
        error = null;
        switch (binding.Scope)
        {
            case TextScope.Report:
                return ReportBag(snapshot);
            case TextScope.Sample:
                var sample = snapshot.Samples.FirstOrDefault(item => item.SampleId == binding.SampleId);
                if (sample is null)
                {
                    error = $"Binding '{binding.BindingId}' names unknown sample '{binding.SampleId}'.";
                    return null;
                }

                return new Dictionary<string, string?>
                {
                    ["reportNumber"] = snapshot.ReportNumber,
                    ["sampleId"] = sample.SampleId,
                    ["sampleCode"] = sample.SampleCode,
                    ["sampleDescription"] = sample.Description,
                    ["matrix"] = sample.Matrix,
                    ["commodity"] = sample.Commodity
                };
            case TextScope.Result:
                var owner = snapshot.Samples.FirstOrDefault(item => item.SampleId == binding.SampleId);
                var result = owner?.Results.FirstOrDefault(item => item.ResultId == binding.ResultId);
                if (owner is null || result is null)
                {
                    error = $"Binding '{binding.BindingId}' does not match a result on sample '{binding.SampleId}'.";
                    return null;
                }

                return new Dictionary<string, string?>
                {
                    ["reportNumber"] = snapshot.ReportNumber,
                    ["sampleId"] = owner.SampleId,
                    ["analyteCode"] = result.AnalyteCode,
                    ["analyteName"] = result.AnalyteName,
                    ["resultDisplay"] = ResultDisplay.Format(result),
                    ["unit"] = result.Unit,
                    ["methodCode"] = result.MethodCode
                };
            case TextScope.Method:
                if (binding.MethodCode is null || !methods.TryGetValue(binding.MethodCode, out var method))
                {
                    error = $"Binding '{binding.BindingId}' names unknown method '{binding.MethodCode}'.";
                    return null;
                }

                return new Dictionary<string, string?>
                {
                    ["methodCode"] = method.MethodCode,
                    ["methodName"] = method.MethodName,
                    ["referenceCitation"] = method.ReferenceCitation
                };
            default:
                error = $"Binding '{binding.BindingId}' uses a scope that cannot be attached to data.";
                return null;
        }
    }
}

public static class MethodIndex
{
    public static int? Number(RenderPlan plan, string? methodCode)
    {
        if (methodCode is null)
        {
            return null;
        }

        return plan.Methods.FirstOrDefault(method => method.Method.MethodCode == methodCode)?.Number;
    }
}
