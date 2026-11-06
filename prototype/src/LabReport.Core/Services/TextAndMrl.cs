using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using LabReport.Core.Models;

namespace LabReport.Core.Services;

public sealed record ResolvedText
{
    public required string Key { get; init; }
    public required string VersionLabel { get; init; }
    public required TextScope Scope { get; init; }
    public required TextCategory Category { get; init; }
    public string? SampleId { get; init; }
    public string? ResultId { get; init; }
    public string? MethodCode { get; init; }
    public string? SectionInstanceId { get; init; }
    public required string PlainText { get; init; }
}

public static class TextResolver
{
    private static readonly Regex Token = new(@"\{\{\s*([A-Za-z][A-Za-z0-9]*)\s*\}\}", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static readonly IReadOnlyDictionary<TextScope, string[]> AllowedTokens = new Dictionary<TextScope, string[]>
    {
        [TextScope.Report] = ["reportNumber", "reportDate", "sampleCount", "clientName", "matrix", "commodity", "title"],
        [TextScope.Sample] = ["reportNumber", "sampleId", "sampleCode", "sampleDescription", "matrix", "commodity"],
        [TextScope.Result] = ["reportNumber", "sampleId", "analyteCode", "analyteName", "resultDisplay", "unit", "methodCode"],
        [TextScope.Method] = ["methodCode", "methodName", "referenceCitation"],
        [TextScope.Section] = ["sectionTitle", "reportNumber", "signatoryName", "signatoryRole", "signatoryStatement"]
    };

    public static TextEntry? Select(IEnumerable<TextEntry> entries, string key, string language, DateOnly asOf)
    {
        return entries
            .Where(entry => string.Equals(entry.Key, key, StringComparison.Ordinal)
                && string.Equals(entry.Language, language, StringComparison.Ordinal)
                && entry.ApprovalStatus == ApprovalStatus.Approved
                && entry.EffectiveFrom <= asOf
                && (entry.EffectiveTo is null || entry.EffectiveTo >= asOf))
            .OrderByDescending(entry => entry.EffectiveFrom)
            .ThenByDescending(entry => entry.VersionLabel, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    public static (string? Text, string? Error) Resolve(TextEntry entry, IReadOnlyDictionary<string, string?> values)
    {
        if (!AllowedTokens.TryGetValue(entry.Scope, out var allowed))
        {
            return (null, $"Text '{entry.Key}' uses an unknown scope.");
        }

        var allow = new HashSet<string>(allowed, StringComparer.Ordinal);
        var body = entry.Body ?? "";
        if (body.Contains('<') || body.Contains('>'))
        {
            return (null, $"Text '{entry.Key}' contains raw markup. HTML in the catalog is disabled.");
        }

        var cursor = 0;
        var builder = new StringBuilder();
        foreach (Match match in Token.Matches(body))
        {
            builder.Append(body.AsSpan(cursor, match.Index - cursor));
            var name = match.Groups[1].Value;
            if (!allow.Contains(name))
            {
                return (null, $"Text '{entry.Key}' uses placeholder '{name}', which is not allowed for scope {entry.Scope}.");
            }

            if (!values.TryGetValue(name, out var raw) || string.IsNullOrWhiteSpace(raw))
            {
                return (null, $"Text '{entry.Key}' is missing a value for placeholder '{name}'.");
            }

            builder.Append(raw);
            cursor = match.Index + match.Length;
        }

        builder.Append(body.AsSpan(cursor));
        var unresolved = builder.ToString();
        if (unresolved.Contains("{{", StringComparison.Ordinal) || unresolved.Contains("}}", StringComparison.Ordinal))
        {
            return (null, $"Text '{entry.Key}' has an unbalanced placeholder.");
        }

        return (unresolved, null);
    }

    public static string Encode(string value) => WebUtility.HtmlEncode(value);
}

public enum MrlStatus
{
    WithinFictionalLimit,
    ExceedsFictionalLimit,
    BelowLoqNotExceedingLimit,
    InconclusiveLoqAboveLimit,
    NotDetectedUncompared,
    NotTested,
    MissingValue,
    MissingUnit,
    UnitMismatch,
    NoApplicableLimit,
    AmbiguousLimit
}

public sealed record MrlEvaluation
{
    public required string SampleId { get; init; }
    public required string ResultId { get; init; }
    public required string AnalyteCode { get; init; }
    public required string AnalyteName { get; init; }
    public required string ResultDisplay { get; init; }
    public string? ResultUnit { get; init; }
    public required MrlStatus Status { get; init; }
    public required string StatusLabel { get; init; }
    public string? LimitDisplay { get; init; }
    public string? LimitUnit { get; init; }
    public string? Jurisdiction { get; init; }
    public string? LimitVersion { get; init; }
    public string? LimitSource { get; init; }
    public string? MrlId { get; init; }
    public bool IsNumericComparison { get; init; }
}

public static class MrlEvaluator
{
    public static IReadOnlyList<MrlEvaluation> Evaluate(ReportSnapshot snapshot, SampleSnapshot sample, string jurisdiction, IReadOnlyList<MrlLimit> limits)
    {
        return sample.Results.Select(result => EvaluateOne(snapshot, sample, result, jurisdiction, limits)).ToList();
    }

    public static MrlEvaluation EvaluateOne(ReportSnapshot snapshot, SampleSnapshot sample, ResultSnapshot result, string jurisdiction, IReadOnlyList<MrlLimit> limits)
    {
        var display = ResultDisplay.Format(result);
        MrlEvaluation Base(MrlStatus status, string label, MrlLimit? limit, bool numericComparison) => new()
        {
            SampleId = sample.SampleId,
            ResultId = result.ResultId,
            AnalyteCode = result.AnalyteCode,
            AnalyteName = result.AnalyteName,
            ResultDisplay = display,
            ResultUnit = result.Unit,
            Status = status,
            StatusLabel = label,
            LimitDisplay = limit is null ? null : ResultDisplay.FormatDecimal(limit.LimitValue),
            LimitUnit = limit?.Unit,
            Jurisdiction = limit?.Jurisdiction ?? jurisdiction,
            LimitVersion = limit?.VersionLabel,
            LimitSource = limit?.SourceName,
            MrlId = limit?.MrlId,
            IsNumericComparison = numericComparison
        };

        if (result.Kind == ResultKind.NotTested)
        {
            return Base(MrlStatus.NotTested, "Not tested. No fictional comparison.", null, false);
        }

        if (result.Kind == ResultKind.Missing || (result.Kind == ResultKind.Numeric && result.NumericValue is null))
        {
            return Base(MrlStatus.MissingValue, "No numeric result is on file. No fictional comparison.", null, false);
        }

        var candidates = limits.Where(limit =>
            limit.Fictional
            && string.Equals(limit.AnalyteCode, result.AnalyteCode, StringComparison.Ordinal)
            && string.Equals(limit.Jurisdiction, jurisdiction, StringComparison.Ordinal)
            && string.Equals(limit.Matrix, sample.Matrix, StringComparison.Ordinal)
            && string.Equals(limit.Commodity, sample.Commodity, StringComparison.Ordinal)
            && limit.EffectiveFrom <= snapshot.ReportDate
            && (limit.EffectiveTo is null || limit.EffectiveTo >= snapshot.ReportDate))
            .OrderByDescending(limit => limit.EffectiveFrom)
            .ThenBy(limit => limit.MrlId, StringComparer.Ordinal)
            .ToList();

        if (candidates.Count == 0)
        {
            return Base(MrlStatus.NoApplicableLimit, "No applicable fictional limit.", null, false);
        }

        var newest = candidates[0].EffectiveFrom;
        var tied = candidates.Where(limit => limit.EffectiveFrom == newest).ToList();
        if (tied.Count > 1)
        {
            return Base(MrlStatus.AmbiguousLimit, "More than one fictional limit matches. No comparison was made.", null, false);
        }

        var match = tied[0];
        if (string.IsNullOrWhiteSpace(result.Unit) || UnitLexicon.Canonical(result.Unit) is null)
        {
            return Base(MrlStatus.MissingUnit, "The result has no known unit, so it was not compared.", match, false);
        }

        if (!UnitLexicon.Compatible(result.Unit, match.Unit))
        {
            return Base(MrlStatus.UnitMismatch, "Units differ and this demo does not convert them. No pass or fail was assigned.", match, false);
        }

        if (result.Kind == ResultKind.NotDetected)
        {
            return Base(MrlStatus.NotDetectedUncompared, "Reported as ND. ND is not treated as a numeric pass or fail.", match, false);
        }

        if (result.Kind == ResultKind.BelowLoq)
        {
            if (result.Loq is not decimal loq)
            {
                return Base(MrlStatus.InconclusiveLoqAboveLimit, "Below LOQ without an LOQ value. No comparison was made.", match, false);
            }

            if (loq <= match.LimitValue)
            {
                return Base(MrlStatus.BelowLoqNotExceedingLimit, "Below LOQ. The LOQ does not exceed the fictional limit. This is not a numeric pass.", match, false);
            }

            return Base(MrlStatus.InconclusiveLoqAboveLimit, "Below LOQ, and the LOQ is above the fictional limit. The comparison is inconclusive.", match, false);
        }

        var value = result.NumericValue!.Value;
        if (value <= match.LimitValue)
        {
            return Base(MrlStatus.WithinFictionalLimit, "Numeric value is within the fictional limit.", match, true);
        }

        return Base(MrlStatus.ExceedsFictionalLimit, "Numeric value exceeds the fictional limit.", match, true);
    }
}

public static class MrlCatalogValidator
{
    private static readonly Regex Jurisdiction = new("^FICTION-[A-Z0-9-]{1,40}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static IReadOnlyList<ValidationIssue> Validate(IEnumerable<MrlLimit> limits)
    {
        var issues = new List<ValidationIssue>();
        foreach (var limit in limits)
        {
            if (!limit.Fictional)
            {
                issues.Add(new("mrl.not-fictional", $"Limit {limit.MrlId} is not marked fictional."));
            }

            if (!Jurisdiction.IsMatch(limit.Jurisdiction))
            {
                issues.Add(new("mrl.jurisdiction", $"Limit {limit.MrlId} must use a FICTION- jurisdiction code."));
            }

            if (limit.SourceName.Contains("fictional", StringComparison.OrdinalIgnoreCase) == false)
            {
                issues.Add(new("mrl.source", $"Limit {limit.MrlId} source name must say that it is fictional."));
            }

            if (UnitLexicon.Canonical(limit.Unit) is null)
            {
                issues.Add(new("mrl.unit", $"Limit {limit.MrlId} uses an unknown unit."));
            }
        }

        return issues;
    }
}
