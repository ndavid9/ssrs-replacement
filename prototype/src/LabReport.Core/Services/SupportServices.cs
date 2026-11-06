using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using LabReport.Core.Models;

namespace LabReport.Core.Services;

public static class UnitLexicon
{
    private static readonly HashSet<string> Known = new(StringComparer.Ordinal)
    {
        "mg/kg", "ug/kg", "g/kg", "mg/l", "%"
    };

    public static string? Canonical(string? unit)
    {
        if (string.IsNullOrWhiteSpace(unit))
        {
            return null;
        }

        var normalized = unit.Trim().Replace("µ", "u", StringComparison.Ordinal).Replace("μ", "u", StringComparison.Ordinal).ToLowerInvariant();
        return Known.Contains(normalized) ? normalized : null;
    }

    public static bool Compatible(string? left, string? right)
    {
        var a = Canonical(left);
        var b = Canonical(right);
        return a is not null && a == b;
    }
}

public static class ResultDisplay
{
    public static string Format(ResultSnapshot result)
    {
        return result.Kind switch
        {
            ResultKind.Numeric => result.NumericValue?.ToString("0.####", CultureInfo.InvariantCulture) ?? "—",
            ResultKind.BelowLoq when result.Loq is decimal loq => "< " + loq.ToString("0.####", CultureInfo.InvariantCulture),
            ResultKind.BelowLoq => result.Qualifier ?? "< LOQ",
            ResultKind.NotDetected => "ND",
            ResultKind.NotTested => "Not tested",
            ResultKind.Missing => "—",
            _ => "—"
        };
    }

    public static string FormatDecimal(decimal? value) =>
        value?.ToString("0.####", CultureInfo.InvariantCulture) ?? "—";
}

public static class PathSafety
{
    private static readonly Regex SafeId = new("^[A-Za-z0-9][A-Za-z0-9._-]{0,80}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static bool IsSafeId(string? id) => !string.IsNullOrWhiteSpace(id) && SafeId.IsMatch(id);

    public static string CombineUnderRoot(string root, params string[] parts)
    {
        var fullRoot = Path.GetFullPath(root);
        var combined = fullRoot;
        foreach (var part in parts)
        {
            if (string.IsNullOrWhiteSpace(part) || part.Contains("..", StringComparison.Ordinal) || Path.IsPathRooted(part))
            {
                throw new InvalidOperationException("A storage path part is not allowed.");
            }

            combined = Path.GetFullPath(Path.Combine(combined, part));
        }

        var prefix = fullRoot.EndsWith(Path.DirectorySeparatorChar) ? fullRoot : fullRoot + Path.DirectorySeparatorChar;
        if (!combined.StartsWith(prefix, StringComparison.Ordinal) && !string.Equals(combined, fullRoot, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The path escapes the storage root.");
        }

        return combined;
    }
}

public static class SnapshotHasher
{
    public static string Hash<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, JsonDefaults.Canonical);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static string HashBytes(byte[] value) => Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();
}

public static class SampleGrouper
{
    public const int MaxSamplesPerPage = 20;

    public static IReadOnlyList<SampleGroup> Group(IReadOnlyList<SampleSnapshot> samples, int samplesPerPage)
    {
        if (samplesPerPage < 1 || samplesPerPage > MaxSamplesPerPage)
        {
            throw new ArgumentOutOfRangeException(nameof(samplesPerPage), "Samples per page must be from 1 to 20.");
        }

        var ordered = samples.OrderBy(s => s.Ordinal).ThenBy(s => s.SampleId, StringComparer.Ordinal).ToList();
        var groups = new List<SampleGroup>();
        for (var index = 0; index < ordered.Count; index += samplesPerPage)
        {
            var slice = ordered.Skip(index).Take(samplesPerPage).ToList();
            groups.Add(new SampleGroup(groups.Count + 1, slice));
        }

        return groups;
    }
}

public sealed record SampleGroup(int Number, IReadOnlyList<SampleSnapshot> Samples);

public static class FeedContracts
{
    public const string ReportSnapshotV1 = "report-snapshot-v1";

    public static FeedContract ReportSnapshot { get; } = new(
        Name: ReportSnapshotV1,
        DisplayName: "Report snapshot v1",
        ObjectKind: "stored-procedure",
        SqlObject: "rpt.usp_ExportReportSnapshot",
        Parameters: ["@TenantId", "@ReportId", "@PrincipalId"],
        Fields:
        [
            new("schemaVersion", "Contract version. Currently 1.0.", true),
            new("tenantId", "Tenant that owns the report. The export procedure filters on it.", true),
            new("reportId", "Stable report key.", true),
            new("reportNumber", "Human report number.", true),
            new("samples", "Ordered samples. Results are nested under the owning sample id.", true),
            new("methods", "Method references cited by results.", true),
            new("textBindings", "Scoped pointers into the approved text catalog.", true)
        ]);

    private static readonly Regex ContractName = new("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static bool TryGet(string? name, out FeedContract contract)
    {
        if (string.Equals(name, ReportSnapshotV1, StringComparison.Ordinal))
        {
            contract = ReportSnapshot;
            return true;
        }

        contract = null!;
        return false;
    }

    public static bool IsSafeContractName(string? name) =>
        !string.IsNullOrWhiteSpace(name) && ContractName.IsMatch(name) && !LooksHostile(name);

    public static bool LooksHostile(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var text = value.Trim();
        if (text.Contains("://", StringComparison.Ordinal) || text.Contains('\\') || text.Contains(';') || text.Contains("--", StringComparison.Ordinal))
        {
            return true;
        }

        return Regex.IsMatch(text, @"\b(select|insert|update|delete|drop|exec|execute|union|alter|create)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}

public static class SectionCatalog
{
    private static readonly string[] ResultColumns = ["analyte", "result", "qualifier", "unit", "lod", "loq", "method"];
    private static readonly string[] MrlColumns = ["analyte", "result", "unit", "limit", "limitUnit", "jurisdiction", "evaluation", "source"];

    private static readonly IReadOnlyDictionary<string, SectionTypeInfo> Types = new Dictionary<string, SectionTypeInfo>(StringComparer.Ordinal)
    {
        ["report-metadata"] = new("report-metadata", "Report metadata", SectionPlacement.Report, "Identifiers, client, dates, and the sample index.", []),
        ["sample-summary"] = new("sample-summary", "Sample summary", SectionPlacement.Sample, "Identity and reception fields for one sample.", []),
        ["analytical-results"] = new("analytical-results", "Analytical results", SectionPlacement.Sample, "Result tables grouped into subsections.", ResultColumns),
        ["mrl-comparison"] = new("mrl-comparison", "Fictional limit comparison", SectionPlacement.Sample, "Compares each result with a fictional limit when the unit and scope match.", MrlColumns),
        ["comments"] = new("comments", "Comments", SectionPlacement.Sample, "Sample-scoped comments attached to the sample id.", []),
        ["conclusions"] = new("conclusions", "Conclusions", SectionPlacement.Sample, "Sample-scoped conclusions. Report-scoped conclusions print once after the samples.", []),
        ["method-references"] = new("method-references", "Method references", SectionPlacement.Report, "Deduplicated method notes, distinct from the result values.", []),
        ["disclaimers"] = new("disclaimers", "Disclaimers", SectionPlacement.Report, "Report-level disclaimer, printed once.", []),
        ["approval-signature"] = new("approval-signature", "Approval label", SectionPlacement.Report, "Structured signatory block selected by id. Not a cryptographic signature.", [])
    };

    public static IReadOnlyList<SectionTypeInfo> All { get; } = Types.Values.ToList();

    public static bool IsKnown(string? type) => type is not null && Types.ContainsKey(type);

    public static SectionTypeInfo Get(string type) => Types[type];

    public static SectionConfig CreateDefault(string type)
    {
        var info = Get(type);
        var columns = info.ColumnKeys.Select(key => new ColumnConfig { Key = key, Label = DefaultLabel(key), Visible = true }).ToList();
        var subsections = type == "analytical-results"
            ? new List<SubsectionConfig>
            {
                new() { Id = "metals", Title = "Metals", Order = 10, Enabled = true, AnalyteGroup = "metals" },
                new() { Id = "screen", Title = "Screen", Order = 20, Enabled = true, AnalyteGroup = "screen" }
            }
            : [];
        var textKeys = type switch
        {
            "disclaimers" => new List<string> { "disclaimer.scope" },
            "approval-signature" => new List<string> { "approval.demo-block" },
            _ => new List<string>()
        };
        return new SectionConfig
        {
            InstanceId = type,
            Type = type,
            Title = info.DefaultTitle,
            Order = 0,
            Enabled = true,
            Columns = columns,
            Subsections = subsections,
            TextKeys = textKeys
        };
    }

    public static string DefaultLabel(string key) => key switch
    {
        "analyte" => "Analyte",
        "result" => "Result",
        "qualifier" => "Qualifier",
        "unit" => "Unit",
        "lod" => "LOD",
        "loq" => "LOQ",
        "method" => "Method",
        "limit" => "Fictional limit",
        "limitUnit" => "Limit unit",
        "jurisdiction" => "Jurisdiction",
        "evaluation" => "Evaluation",
        "source" => "Limit source",
        _ => key
    };
}

public sealed record SectionTypeInfo(
    string Type,
    string DefaultTitle,
    SectionPlacement Placement,
    string Description,
    IReadOnlyList<string> ColumnKeys);

public static class Columns
{
    public static bool Visible(SectionConfig section, string key)
    {
        if (section.Columns.Count == 0)
        {
            return true;
        }

        var column = section.Columns.FirstOrDefault(c => string.Equals(c.Key, key, StringComparison.Ordinal));
        return column is not null && column.Visible;
    }

    public static string Label(SectionConfig section, string key)
    {
        var column = section.Columns.FirstOrDefault(c => string.Equals(c.Key, key, StringComparison.Ordinal));
        if (column is not null && !string.IsNullOrWhiteSpace(column.Label))
        {
            return column.Label;
        }

        return SectionCatalog.DefaultLabel(key);
    }

    public static int VisibleCount(SectionConfig section, IReadOnlyList<string> keys) =>
        keys.Count(key => Visible(section, key));
}
