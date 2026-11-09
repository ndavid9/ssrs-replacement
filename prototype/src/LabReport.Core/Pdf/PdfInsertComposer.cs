using System.Globalization;
using System.Text;
using LabReport.Core.Models;
using LabReport.Core.Rendering;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace LabReport.Core.Pdf;

public sealed record InsertPayload(string Id, PdfInsertMode Mode, string Label, int PageCount, byte[] Pdf);

public sealed record MergeResult(byte[] Pdf, NumberingPlan Plan);

public static class PdfInsertComposer
{
    public static MergeResult Merge(byte[] rendered, IReadOnlyList<InsertPayload> payloads)
    {
        if (payloads.Count == 0)
        {
            return new MergeResult(rendered, InsertNumbering.ReportOnly(CountPages(rendered)));
        }

        var slots = new List<SlotHit>();
        foreach (var payload in payloads)
        {
            var hits = PdfTextLocator.Find(rendered, InsertMarkers.Slot(payload.Id));
            if (hits.Count != 1)
            {
                throw new InvalidOperationException($"Insert '{payload.Id}' was found on {hits.Count.ToString(CultureInfo.InvariantCulture)} rendered pages. Expected one slot page.");
            }

            slots.Add(new SlotHit(payload.Id, hits[0].PageIndex));
        }

        var plan = InsertNumbering.Build(
            CountPages(rendered),
            slots,
            payloads.Select(payload => new InsertSpec(payload.Id, payload.Mode, payload.Label, payload.PageCount)).ToList());

        var streams = new List<MemoryStream>();
        var documents = new List<PdfDocument>();
        try
        {
            var renderedDocument = Open(rendered, streams, documents);
            var sources = new Dictionary<string, PdfDocument>(StringComparer.Ordinal);
            foreach (var payload in payloads)
            {
                var source = Open(payload.Pdf, streams, documents);
                if (source.SecuritySettings.IsEncrypted)
                {
                    throw new InvalidOperationException($"Insert '{payload.Id}' is encrypted.");
                }

                if (source.Pages.Count != payload.PageCount)
                {
                    throw new InvalidOperationException($"Insert '{payload.Id}' page count changed between inspection and merge.");
                }

                sources[payload.Id] = source;
            }

            var output = new PdfDocument();
            documents.Add(output);
            var slotAt = slots.ToDictionary(slot => slot.PageIndex, slot => slot.InsertId);
            for (var index = 0; index < renderedDocument.Pages.Count; index++)
            {
                if (!slotAt.TryGetValue(index, out var insertId))
                {
                    output.AddPage(renderedDocument.Pages[index]);
                    continue;
                }

                var source = sources[insertId];
                for (var page = 0; page < source.Pages.Count; page++)
                {
                    output.AddPage(source.Pages[page]);
                }
            }

            if (output.Pages.Count != plan.Pages.Count)
            {
                throw new InvalidOperationException("The merged page count does not match the numbering plan.");
            }

            return new MergeResult(Save(output), plan);
        }
        finally
        {
            foreach (var document in documents)
            {
                document.Dispose();
            }

            foreach (var stream in streams)
            {
                stream.Dispose();
            }
        }
    }

    public static byte[] Stamp(byte[] merged, NumberingPlan plan, byte[] font)
    {
        ReportFonts.Ensure(font);
        var ranges = new Dictionary<string, (GlyphHit Hit, string Text)>(StringComparer.Ordinal);
        foreach (var (id, text) in plan.AppendixRanges)
        {
            var hits = PdfTextLocator.Find(merged, InsertMarkers.Range(id));
            if (hits.Count != 1)
            {
                throw new InvalidOperationException($"Appendix range for '{id}' was found {hits.Count.ToString(CultureInfo.InvariantCulture)} times in the merged report.");
            }

            ranges[id] = (hits[0], text);
        }

        var input = new MemoryStream(merged);
        try
        {
            using var document = PdfReader.Open(input, PdfDocumentOpenMode.Modify);
            if (document.Pages.Count != plan.Pages.Count)
            {
                throw new InvalidOperationException("The merged PDF page count does not match the numbering plan.");
            }

            var face = new XFont("Liberation Sans", 8, XFontStyleEx.Regular);
            for (var index = 0; index < document.Pages.Count; index++)
            {
                var page = document.Pages[index];
                var composed = plan.Pages[index];
                if (composed.Kind != ComposedPageKind.Report)
                {
                    ExpandBottomStrip(page);
                }

                using var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
                if (composed.Kind == ComposedPageKind.Report)
                {
                    DrawFooterNumber(gfx, page, composed.ReportStamp ?? throw new InvalidOperationException("A report page is missing its number."), face);
                    foreach (var (_, range) in ranges)
                    {
                        if (range.Hit.PageIndex == index)
                        {
                            DrawRange(gfx, page, range.Hit, range.Text, face);
                        }
                    }
                }
                else if (composed.Kind == ComposedPageKind.Attachment)
                {
                    DrawStrip(gfx, page, composed.AttachmentStamp ?? throw new InvalidOperationException("An attachment page is missing its label."), face, alignRight: false);
                }
                else
                {
                    DrawStrip(gfx, page, composed.ReportStamp ?? throw new InvalidOperationException("An appendix page is missing its number."), face, alignRight: true);
                }
            }

            using var output = new MemoryStream();
            document.Save(output, false);
            return output.ToArray();
        }
        finally
        {
            input.Dispose();
        }
    }

    private static int CountPages(byte[] pdf)
    {
        using var stream = new MemoryStream(pdf, writable: false);
        using var document = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
        return document.Pages.Count;
    }

    private static PdfDocument Open(byte[] pdf, List<MemoryStream> streams, List<PdfDocument> documents)
    {
        var stream = new MemoryStream(pdf);
        streams.Add(stream);
        var document = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
        documents.Add(document);
        return document;
    }

    private static byte[] Save(PdfDocument document)
    {
        using var stream = new MemoryStream();
        document.Save(stream, false);
        return stream.ToArray();
    }

    /// <summary>
    /// Adds a 10 mm band on the visual bottom without rewriting the original content operators.
    /// The media box grows upward and the existing content is translated up by the same amount,
    /// so the new band is empty and the label cannot land on the uploaded page.
    /// </summary>
    private static void ExpandBottomStrip(PdfPage page)
    {
        var strip = PageStampMetrics.Points(PageStampMetrics.InsertStripMm);
        page.MediaBox = Taller(page.MediaBox, strip);
        if (page.HasCropBox)
        {
            page.CropBox = Taller(page.CropBox, strip);
        }

        if (page.HasBleedBox)
        {
            page.BleedBox = Taller(page.BleedBox, strip);
        }

        if (page.HasTrimBox)
        {
            page.TrimBox = Taller(page.TrimBox, strip);
        }

        if (page.HasArtBox)
        {
            page.ArtBox = Taller(page.ArtBox, strip);
        }

        var amount = strip.ToString("0.###", CultureInfo.InvariantCulture);
        var shift = page.Contents.PrependContent();
        shift.CreateStream(Encoding.ASCII.GetBytes("q\n1 0 0 1 0 " + amount + " cm\n"));
        var restore = page.Contents.AppendContent();
        restore.CreateStream("Q\n"u8.ToArray());
    }

    private static PdfRectangle Taller(PdfRectangle box, double strip)
    {
        var left = Math.Min(box.X1, box.X2);
        var bottom = Math.Min(box.Y1, box.Y2);
        var right = Math.Max(box.X1, box.X2);
        var top = Math.Max(box.Y1, box.Y2);
        return new PdfRectangle(new XPoint(left, bottom), new XPoint(right, top + strip));
    }

    private static void DrawFooterNumber(XGraphics gfx, PdfPage page, string text, XFont font)
    {
        var boxWidth = PageStampMetrics.Points(PageStampMetrics.BoxWidthMm);
        var boxHeight = PageStampMetrics.Points(PageStampMetrics.BoxHeightMm);
        var right = PageStampMetrics.Points(PageStampMetrics.RightInsetMm);
        var bottom = PageStampMetrics.Points(PageStampMetrics.BottomInsetMm);
        var rect = new XRect(page.Width.Point - right - boxWidth, page.Height.Point - bottom - boxHeight, boxWidth, boxHeight);
        var fill = new XRect(rect.X + 0.8, rect.Y + 0.8, Math.Max(1, rect.Width - 1.6), Math.Max(1, rect.Height - 1.6));
        gfx.DrawRectangle(XBrushes.White, fill);
        gfx.DrawString(text, font, XBrushes.Black, rect, XStringFormats.Center);
    }

    private static void DrawStrip(XGraphics gfx, PdfPage page, string text, XFont font, bool alignRight)
    {
        var strip = PageStampMetrics.Points(PageStampMetrics.InsertStripMm);
        var width = page.Width.Point;
        var top = page.Height.Point - strip;
        gfx.DrawRectangle(XBrushes.White, new XRect(0, top, width, strip));
        gfx.DrawLine(new XPen(XColor.FromArgb(154, 163, 173), 0.6), 8, top, width - 8, top);
        var rect = new XRect(10, top + 1, width - 20, strip - 2);
        gfx.DrawString(text, font, XBrushes.Black, rect, alignRight ? XStringFormats.CenterRight : XStringFormats.CenterLeft);
    }

    private static void DrawRange(XGraphics gfx, PdfPage page, GlyphHit hit, string text, XFont font)
    {
        var media = page.MediaBox;
        var left = Math.Min(hit.Left, hit.Right) - 0.8;
        var right = Math.Max(hit.Left, hit.Right) + 0.8;
        var bottom = Math.Min(hit.Bottom, hit.Top) - 0.8;
        var top = Math.Max(hit.Bottom, hit.Top) + 0.8;
        var originX = Math.Min(media.X1, media.X2);
        var originTop = Math.Max(media.Y1, media.Y2);
        var rect = new XRect(left - originX, originTop - top, right - left, top - bottom);
        gfx.DrawRectangle(XBrushes.White, rect);
        gfx.DrawString(text, font, new XSolidBrush(XColor.FromArgb(28, 36, 48)), rect, XStringFormats.Center);
    }
}

public static class SlotFixture
{
    public static byte[] Rendered(byte[] font, IReadOnlyList<string> pages)
    {
        ReportFonts.Ensure(font);
        var document = new PdfDocument();
        var face = new XFont("Liberation Sans", 12, XFontStyleEx.Regular);
        foreach (var pageText in pages)
        {
            var page = document.AddPage();
            page.Width = XUnit.FromMillimeter(210);
            page.Height = XUnit.FromMillimeter(297);
            using var gfx = XGraphics.FromPdfPage(page);
            var top = 48.0;
            foreach (var line in pageText.Split('\n'))
            {
                gfx.DrawString(line, face, XBrushes.Black, new XRect(36, top, page.Width.Point - 72, 18), XStringFormats.TopLeft);
                top += 22;
            }
        }

        using var stream = new MemoryStream();
        document.Save(stream, false);
        document.Dispose();
        return stream.ToArray();
    }
}
