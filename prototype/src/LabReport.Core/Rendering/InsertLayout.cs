using System.Globalization;
using System.Text.RegularExpressions;
using LabReport.Core.Models;
using LabReport.Core.Services;

namespace LabReport.Core.Rendering;

public sealed record InsertPageInfo(double WidthPt, double HeightPt, int Rotation)
{
    public double DisplayedWidth => Rotation is 90 or 270 ? HeightPt : WidthPt;
    public double DisplayedHeight => Rotation is 90 or 270 ? WidthPt : HeightPt;
    public bool Landscape => DisplayedWidth > DisplayedHeight + 0.5;
}

public sealed record ReportInsert
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public required PdfInsertMode Mode { get; init; }
    public required string Slot { get; init; }
    public required string FileName { get; init; }
    public required string Sha256 { get; init; }
    public required int PageCount { get; init; }
    public required IReadOnlyList<InsertPageInfo> Pages { get; init; }

    public string Marker => InsertMarkers.Slot(Id);
    public string RangeToken => InsertMarkers.Range(Id);

    public string OrientationText
    {
        get
        {
            var landscape = new List<int>();
            for (var index = 0; index < Pages.Count; index++)
            {
                if (Pages[index].Landscape)
                {
                    landscape.Add(index + 1);
                }
            }

            if (landscape.Count == 0)
            {
                return "Portrait";
            }

            if (landscape.Count == Pages.Count)
            {
                return Pages.Count == 1 ? "Landscape" : "Landscape throughout";
            }

            return "Mixed, landscape " + string.Join(", ", landscape.Select(number => "page " + number.ToString(CultureInfo.InvariantCulture)));
        }
    }
}

public sealed record InsertSlotView(ReportInsert Insert, double HeightMm);

public static class InsertMarkers
{
    public static string Slot(string id) => "INSERT-SLOT:" + id;

    public static string Range(string id) => "APXRANGE_" + id.Replace('-', '_').ToUpperInvariant();

    public static string ReportNumber(int number, int total) =>
        "Page " + number.ToString(CultureInfo.InvariantCulture) + " of " + total.ToString(CultureInfo.InvariantCulture);

    public static string AttachmentLabel(string label, int page, int total) =>
        label + " - page " + page.ToString(CultureInfo.InvariantCulture) + " of " + total.ToString(CultureInfo.InvariantCulture);

    public static string AppendixRange(int start, int end) =>
        start.ToString(CultureInfo.InvariantCulture) + "-" + end.ToString(CultureInfo.InvariantCulture);
}

public static class InsertAnchors
{
    public static IEnumerable<ReportInsert> After(RenderPlan plan, string slot) =>
        plan.Inserts.Where(insert => string.Equals(insert.Slot, slot, StringComparison.Ordinal));

    public static double SlotHeightMm(TemplateDefinition template)
    {
        var longEdge = template.Paper == "Letter" ? 279.4 : 297.0;
        var shortEdge = template.Paper == "Letter" ? 215.9 : 210.0;
        var page = template.Orientation == "landscape" ? shortEdge : longEdge;
        var content = page - template.MarginTopMm - template.MarginBottomMm;
        return Math.Max(40, content - 6);
    }
}

public static class PageStampMetrics
{
    public const double BoxWidthMm = 42;
    public const double BoxHeightMm = 6;
    public const double RightInsetMm = 8;
    public const double BottomInsetMm = 5;
    public const double InsertStripMm = 10;

    public static double Points(double millimeters) => millimeters * 72.0 / 25.4;

    public static string FooterHtml()
    {
        var culture = CultureInfo.InvariantCulture;
        return "<div style=\"font-size:9px;width:100%;height:100%;font-family:sans-serif;color:#1c2430;position:relative;\">"
            + "<span style=\"position:absolute;left:8mm;bottom:" + BottomInsetMm.ToString(culture) + "mm;\">Synthetic demo. No accreditation. No cryptographic signature.</span>"
            + "<span style=\"position:absolute;right:" + RightInsetMm.ToString(culture)
            + "mm;bottom:" + BottomInsetMm.ToString(culture)
            + "mm;width:" + BoxWidthMm.ToString(culture)
            + "mm;height:" + BoxHeightMm.ToString(culture)
            + "mm;border:0.6px solid #9aa3ad;box-sizing:border-box;\"></span></div>";
    }
}

public static class InsertRules
{
    private static readonly Regex Id = new("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex FileName = new("^[A-Za-z0-9][A-Za-z0-9._-]{0,64}\\.pdf$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static IReadOnlyList<ValidationIssue> Validate(TemplateDefinition template)
    {
        var issues = new List<ValidationIssue>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var sections = template.Sections.ToDictionary(section => section.InstanceId, StringComparer.Ordinal);
        foreach (var insert in template.Inserts)
        {
            if (string.IsNullOrWhiteSpace(insert.Id) || !Id.IsMatch(insert.Id))
            {
                issues.Add(new("insert.id", "An insert id must be lowercase letters, digits, and hyphens."));
            }
            else if (!seen.Add(insert.Id))
            {
                issues.Add(new("insert.duplicate", $"Insert id '{insert.Id}' is used more than once."));
            }

            if (!Enum.IsDefined(insert.Mode))
            {
                issues.Add(new("insert.mode", $"Insert '{insert.Id}' has an unknown mode."));
            }

            if (string.IsNullOrWhiteSpace(insert.Label) || insert.Label.Trim().Length > 80 || insert.Label.Contains('<') || insert.Label.Contains('>'))
            {
                issues.Add(new("insert.label", $"Insert '{insert.Id}' needs a plain label of at most 80 characters."));
            }

            if (string.IsNullOrWhiteSpace(insert.File) || !FileName.IsMatch(insert.File))
            {
                issues.Add(new("insert.file", $"Insert '{insert.Id}' must name a single PDF file in the inserts folder."));
            }

            if (string.Equals(insert.Slot, "end", StringComparison.Ordinal))
            {
                continue;
            }

            const string prefix = "after:";
            if (insert.Slot is null || !insert.Slot.StartsWith(prefix, StringComparison.Ordinal))
            {
                issues.Add(new("insert.slot", $"Insert '{insert.Id}' must use slot 'end' or 'after:<section instance id>'."));
                continue;
            }

            var instanceId = insert.Slot[prefix.Length..];
            if (!sections.TryGetValue(instanceId, out var section))
            {
                issues.Add(new("insert.slot", $"Insert '{insert.Id}' is anchored to '{instanceId}', which is not on this template."));
                continue;
            }

            if (!section.Enabled)
            {
                issues.Add(new("insert.disabled", $"Insert '{insert.Id}' is anchored to '{instanceId}', which is disabled, so no slot page would be emitted."));
            }
            else if (!SectionCatalog.IsKnown(section.Type))
            {
                issues.Add(new("insert.slot", $"Insert '{insert.Id}' is anchored to a section type that is not rendered."));
            }
        }

        return issues;
    }

    public static IReadOnlyList<ValidationIssue> Match(IReadOnlyList<PdfInsert> configured, IReadOnlyList<ReportInsert> resolved)
    {
        var issues = new List<ValidationIssue>();
        if (configured.Count != resolved.Count)
        {
            issues.Add(new("insert.files", "The inspected insert list does not match the template."));
            return issues;
        }

        for (var index = 0; index < configured.Count; index++)
        {
            var expected = configured[index];
            var actual = resolved[index];
            if (!string.Equals(expected.Id, actual.Id, StringComparison.Ordinal)
                || expected.Mode != actual.Mode
                || !string.Equals(expected.Slot, actual.Slot, StringComparison.Ordinal)
                || !string.Equals(expected.Label, actual.Label, StringComparison.Ordinal)
                || !string.Equals(expected.File, actual.FileName, StringComparison.Ordinal)
                || actual.PageCount < 1
                || string.IsNullOrWhiteSpace(actual.Sha256))
            {
                issues.Add(new("insert.files", $"Insert '{expected.Id}' did not match its inspected file."));
            }
        }

        return issues;
    }
}

public enum ComposedPageKind
{
    Report,
    Attachment,
    Appendix
}

public sealed record ComposedPage(
    ComposedPageKind Kind,
    int? ReportNumber,
    int ReportTotal,
    string? InsertId,
    string? Label,
    int? InsertPage,
    int? InsertPageCount)
{
    public string? ReportStamp => ReportNumber is int number ? InsertMarkers.ReportNumber(number, ReportTotal) : null;

    public string? AttachmentStamp =>
        Kind == ComposedPageKind.Attachment && Label is not null && InsertPage is int page && InsertPageCount is int total
            ? InsertMarkers.AttachmentLabel(Label, page, total)
            : null;
}

public sealed record NumberingPlan(IReadOnlyList<ComposedPage> Pages, IReadOnlyDictionary<string, string> AppendixRanges)
{
    public int ReportTotal => Pages.FirstOrDefault()?.ReportTotal ?? 0;
}

public sealed record SlotHit(string InsertId, int PageIndex);

public sealed record InsertSpec(string Id, PdfInsertMode Mode, string Label, int PageCount);

public static class InsertNumbering
{
    public static NumberingPlan ReportOnly(int pageCount)
    {
        if (pageCount < 1)
        {
            throw new InvalidOperationException("A rendered report has no pages to number.");
        }

        var pages = Enumerable.Range(1, pageCount)
            .Select(number => new ComposedPage(ComposedPageKind.Report, number, pageCount, null, null, null, null))
            .ToList();
        return new NumberingPlan(pages, new Dictionary<string, string>(StringComparer.Ordinal));
    }

    public static NumberingPlan Build(int renderedPageCount, IReadOnlyList<SlotHit> slots, IReadOnlyList<InsertSpec> inserts)
    {
        if (renderedPageCount < 1)
        {
            throw new InvalidOperationException("A rendered report has no pages to number.");
        }

        if (slots.Count != inserts.Count)
        {
            throw new InvalidOperationException("Each insert needs exactly one slot page.");
        }

        var byId = new Dictionary<string, InsertSpec>(StringComparer.Ordinal);
        foreach (var insert in inserts)
        {
            if (insert.PageCount < 1)
            {
                throw new InvalidOperationException($"Insert '{insert.Id}' has no pages.");
            }

            if (!byId.TryAdd(insert.Id, insert))
            {
                throw new InvalidOperationException($"Insert '{insert.Id}' is listed twice.");
            }
        }

        var slotByPage = new Dictionary<int, string>();
        var seenSlots = new HashSet<string>(StringComparer.Ordinal);
        foreach (var slot in slots)
        {
            if (slot.PageIndex < 0 || slot.PageIndex >= renderedPageCount)
            {
                throw new InvalidOperationException($"Slot '{slot.InsertId}' is outside the rendered page range.");
            }

            if (!slotByPage.TryAdd(slot.PageIndex, slot.InsertId))
            {
                throw new InvalidOperationException($"Page {slot.PageIndex + 1} contains more than one insert slot.");
            }

            if (!seenSlots.Add(slot.InsertId) || !byId.ContainsKey(slot.InsertId))
            {
                throw new InvalidOperationException($"Slot '{slot.InsertId}' does not match the configured inserts.");
            }
        }

        if (seenSlots.Count != byId.Count)
        {
            var missing = byId.Keys.First(id => !seenSlots.Contains(id));
            throw new InvalidOperationException($"Insert '{missing}' has no slot page in the rendered report.");
        }

        var reportPages = renderedPageCount - slots.Count;
        var appendixPages = inserts.Where(insert => insert.Mode == PdfInsertMode.Appendix).Sum(insert => insert.PageCount);
        var total = reportPages + appendixPages;
        var output = new List<ComposedPage>();
        var number = 0;
        for (var index = 0; index < renderedPageCount; index++)
        {
            if (!slotByPage.TryGetValue(index, out var insertId))
            {
                number++;
                output.Add(new ComposedPage(ComposedPageKind.Report, number, total, null, null, null, null));
                continue;
            }

            var insert = byId[insertId];
            for (var page = 1; page <= insert.PageCount; page++)
            {
                if (insert.Mode == PdfInsertMode.Appendix)
                {
                    number++;
                    output.Add(new ComposedPage(ComposedPageKind.Appendix, number, total, insert.Id, insert.Label, page, insert.PageCount));
                }
                else
                {
                    output.Add(new ComposedPage(ComposedPageKind.Attachment, null, total, insert.Id, insert.Label, page, insert.PageCount));
                }
            }
        }

        var ranges = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var group in output.Where(page => page.Kind == ComposedPageKind.Appendix).GroupBy(page => page.InsertId!, StringComparer.Ordinal))
        {
            var start = group.Min(page => page.ReportNumber!.Value);
            var end = group.Max(page => page.ReportNumber!.Value);
            ranges[group.Key] = InsertMarkers.AppendixRange(start, end);
        }

        return new NumberingPlan(output, ranges);
    }
}
