using System.Text.RegularExpressions;
using LabReport.Core.Models;

namespace LabReport.Core.Services;

public static class SnapshotCanonicalizer
{
    public static ReportSnapshot Canonicalize(ReportSnapshot snapshot)
    {
        var samples = snapshot.Samples
            .OrderBy(sample => sample.Ordinal)
            .ThenBy(sample => sample.SampleId, StringComparer.Ordinal)
            .Select(sample => sample with
            {
                Results = sample.Results
                    .OrderBy(result => result.Ordinal)
                    .ThenBy(result => result.AnalyteCode, StringComparer.Ordinal)
                    .ThenBy(result => result.ResultId, StringComparer.Ordinal)
                    .ToList()
            })
            .ToList();

        var methods = snapshot.Methods
            .OrderBy(method => method.MethodCode, StringComparer.Ordinal)
            .ToList();
        var bindings = snapshot.TextBindings
            .OrderBy(binding => binding.BindingId, StringComparer.Ordinal)
            .ToList();

        return snapshot with
        {
            Samples = samples,
            Methods = methods,
            TextBindings = bindings
        };
    }
}

public static class SnapshotValidator
{
    private static readonly Regex Id = new("^[A-Za-z0-9][A-Za-z0-9._-]{0,80}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static IReadOnlyList<ValidationIssue> Validate(ReportSnapshot snapshot)
    {
        var issues = new List<ValidationIssue>();
        if (snapshot.SchemaVersion != "1.0")
        {
            issues.Add(new("snapshot.schema", "Only snapshot schema 1.0 is supported."));
        }

        if (!string.Equals(snapshot.SourceFeed, FeedContracts.ReportSnapshotV1, StringComparison.Ordinal))
        {
            issues.Add(new("snapshot.feed", "The snapshot source feed is not the approved contract."));
        }

        RequireId(issues, "report", snapshot.ReportId);
        RequireId(issues, "tenant", snapshot.TenantId);
        var sampleIds = new HashSet<string>(StringComparer.Ordinal);
        var ordinals = new HashSet<int>();
        foreach (var sample in snapshot.Samples)
        {
            RequireId(issues, "sample", sample.SampleId);
            if (!sampleIds.Add(sample.SampleId))
            {
                issues.Add(new("sample.duplicate", $"Sample id {sample.SampleId} is duplicated."));
            }

            if (!ordinals.Add(sample.Ordinal))
            {
                issues.Add(new("sample.ordinal", $"Sample ordinal {sample.Ordinal} is duplicated."));
            }

            var resultIds = new HashSet<string>(StringComparer.Ordinal);
            var resultOrdinals = new HashSet<int>();
            var analytes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var result in sample.Results)
            {
                if (!string.Equals(result.SampleId, sample.SampleId, StringComparison.Ordinal))
                {
                    issues.Add(new("result.sample", $"Result {result.ResultId} is nested under {sample.SampleId} but names {result.SampleId}."));
                }

                if (!resultIds.Add(result.ResultId))
                {
                    issues.Add(new("result.duplicate", $"Result id {result.ResultId} is duplicated."));
                }

                if (!resultOrdinals.Add(result.Ordinal))
                {
                    issues.Add(new("result.ordinal", $"Result ordinal {result.Ordinal} is duplicated on {sample.SampleId}."));
                }

                if (!analytes.Add(result.AnalyteCode))
                {
                    issues.Add(new("result.analyte", $"Analyte {result.AnalyteCode} is repeated on {sample.SampleId}."));
                }

                switch (result.Kind)
                {
                    case ResultKind.Numeric when result.NumericValue is null:
                        issues.Add(new("result.numeric", $"Numeric result {result.ResultId} has no value."));
                        break;
                    case ResultKind.BelowLoq when result.Loq is null:
                        issues.Add(new("result.loq", $"Below-LOQ result {result.ResultId} has no LOQ."));
                        break;
                    case ResultKind.NotDetected when result.NumericValue is not null:
                    case ResultKind.NotTested when result.NumericValue is not null:
                    case ResultKind.Missing when result.NumericValue is not null:
                        issues.Add(new("result.kind", $"Result {result.ResultId} is {result.Kind} but also has a numeric value."));
                        break;
                }
            }
        }

        var methodCodes = snapshot.Methods.Select(method => method.MethodCode).ToHashSet(StringComparer.Ordinal);
        foreach (var result in snapshot.Samples.SelectMany(sample => sample.Results))
        {
            if (result.MethodCode is not null && !methodCodes.Contains(result.MethodCode))
            {
                issues.Add(new("result.method", $"Result {result.ResultId} cites unknown method {result.MethodCode}."));
            }
        }

        return issues;
    }

    private static void RequireId(List<ValidationIssue> issues, string name, string value)
    {
        if (!Id.IsMatch(value ?? ""))
        {
            issues.Add(new($"{name}.id", $"{name} id is missing or contains unsupported characters."));
        }
    }
}

public static class TemplateEditor
{
    private static readonly Regex Jurisdiction = new("^FICTION-[A-Z0-9-]{1,40}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly HashSet<string> Papers = new(StringComparer.Ordinal) { "A4", "Letter" };
    private static readonly HashSet<string> Orientations = new(StringComparer.Ordinal) { "portrait", "landscape" };

    public static TemplateDefinition AddSection(TemplateDefinition template, string type)
    {
        if (!SectionCatalog.IsKnown(type))
        {
            throw new InvalidOperationException($"Section type '{type}' is not in the handler whitelist. A new rendering type needs developer code.");
        }

        if (template.Sections.Any(section => section.Type == type))
        {
            throw new InvalidOperationException("This prototype allows one instance of each section type. Reorder or re-enable the existing one.");
        }

        var order = template.Sections.Count == 0 ? 10 : template.Sections.Max(section => section.Order) + 10;
        var section = SectionCatalog.CreateDefault(type) with { Order = order };
        return Bump(template, template.Sections.Append(section).OrderBy(item => item.Order).ToList());
    }

    public static TemplateDefinition RemoveSection(TemplateDefinition template, string instanceId)
    {
        var next = template.Sections.Where(section => section.InstanceId != instanceId).ToList();
        if (next.Count == template.Sections.Count)
        {
            throw new InvalidOperationException("That section is not on the template.");
        }

        return Bump(template, next);
    }

    public static TemplateDefinition Move(TemplateDefinition template, string instanceId, int direction)
    {
        var ordered = template.Sections.OrderBy(section => section.Order).ThenBy(section => section.InstanceId, StringComparer.Ordinal).ToList();
        var index = ordered.FindIndex(section => section.InstanceId == instanceId);
        var swap = index + direction;
        if (index < 0 || swap < 0 || swap >= ordered.Count)
        {
            return template;
        }

        (ordered[index], ordered[swap]) = (ordered[swap], ordered[index]);
        var rewritten = ordered.Select((section, position) => section with { Order = (position + 1) * 10 }).ToList();
        return Bump(template, rewritten);
    }

    public static TemplateDefinition Apply(TemplateDefinition template, TemplateEdit edit)
    {
        if (edit.SamplesPerPage < 1 || edit.SamplesPerPage > SampleGrouper.MaxSamplesPerPage)
        {
            throw new InvalidOperationException("Samples per page must be from 1 to 20.");
        }

        if (edit.ResultRowsPerChunk < 8 || edit.ResultRowsPerChunk > 40)
        {
            throw new InvalidOperationException("Result rows per chunk must be from 8 to 40.");
        }

        if (!Papers.Contains(edit.Paper) || !Orientations.Contains(edit.Orientation))
        {
            throw new InvalidOperationException("Paper or orientation is not in the whitelist.");
        }

        if (!Jurisdiction.IsMatch(edit.Jurisdiction))
        {
            throw new InvalidOperationException("Jurisdiction must be a FICTION- code. This demo will not attach a real regulatory name.");
        }

        if (edit.MarginTopMm is < 18 or > 40 || edit.MarginBottomMm is < 14 or > 30 || edit.MarginSideMm is < 10 or > 25)
        {
            throw new InvalidOperationException("Margins are outside the supported range.");
        }

        var updates = edit.Sections.ToDictionary(section => section.InstanceId, StringComparer.Ordinal);
        var next = new List<SectionConfig>();
        foreach (var section in template.Sections.OrderBy(section => section.Order))
        {
            if (!updates.TryGetValue(section.InstanceId, out var incoming))
            {
                next.Add(section);
                continue;
            }

            if (!SectionCatalog.IsKnown(section.Type))
            {
                throw new InvalidOperationException($"Section '{section.InstanceId}' is not a whitelisted handler.");
            }

            var info = SectionCatalog.Get(section.Type);
            var columns = info.ColumnKeys.Select(key =>
            {
                var posted = incoming.Columns.FirstOrDefault(column => column.Key == key);
                return new ColumnConfig
                {
                    Key = key,
                    Label = string.IsNullOrWhiteSpace(posted?.Label) ? SectionCatalog.DefaultLabel(key) : posted!.Label.Trim(),
                    Visible = posted?.Visible ?? false
                };
            }).ToList();
            var subsections = section.Subsections.Select(subsection =>
            {
                var posted = incoming.Subsections.FirstOrDefault(item => item.Id == subsection.Id);
                return subsection with
                {
                    Title = string.IsNullOrWhiteSpace(posted?.Title) ? subsection.Title : posted!.Title.Trim(),
                    Enabled = posted?.Enabled ?? subsection.Enabled,
                    Order = posted?.Order ?? subsection.Order
                };
            }).OrderBy(subsection => subsection.Order).ToList();

            next.Add(section with
            {
                Title = string.IsNullOrWhiteSpace(incoming.Title) ? section.Title : incoming.Title.Trim(),
                Enabled = incoming.Enabled,
                Columns = columns,
                Subsections = subsections
            });
        }

        return Bump(template with
        {
            Name = string.IsNullOrWhiteSpace(edit.Name) ? template.Name : edit.Name.Trim(),
            SamplesPerPage = edit.SamplesPerPage,
            ResultRowsPerChunk = edit.ResultRowsPerChunk,
            Paper = edit.Paper,
            Orientation = edit.Orientation,
            MarginTopMm = edit.MarginTopMm,
            MarginBottomMm = edit.MarginBottomMm,
            MarginSideMm = edit.MarginSideMm,
            SignatoryId = edit.SignatoryId,
            Jurisdiction = edit.Jurisdiction,
            Language = "en"
        }, next);
    }

    private static TemplateDefinition Bump(TemplateDefinition template, IReadOnlyList<SectionConfig> sections) =>
        template with
        {
            Version = template.Version + 1,
            UpdatedUtc = DateTimeOffset.UtcNow,
            Sections = sections
        };
}

public sealed record TemplateEdit
{
    public required string Name { get; init; }
    public required int SamplesPerPage { get; init; }
    public required int ResultRowsPerChunk { get; init; }
    public required string Paper { get; init; }
    public required string Orientation { get; init; }
    public required int MarginTopMm { get; init; }
    public required int MarginBottomMm { get; init; }
    public required int MarginSideMm { get; init; }
    public required string SignatoryId { get; init; }
    public required string Jurisdiction { get; init; }
    public required IReadOnlyList<SectionEdit> Sections { get; init; }
}

public sealed record SectionEdit
{
    public required string InstanceId { get; init; }
    public required string Title { get; init; }
    public required bool Enabled { get; init; }
    public IReadOnlyList<ColumnConfig> Columns { get; init; } = [];
    public IReadOnlyList<SubsectionConfig> Subsections { get; init; } = [];
}
