using System.Globalization;
using LabReport.Core.Rendering;
using PdfSharp.Pdf.IO;

namespace LabReport.Core.Pdf;

public abstract record PdfInspection
{
    public sealed record Ok(int PageCount, IReadOnlyList<InsertPageInfo> Pages) : PdfInspection;
    public sealed record Rejected(string Code, string Message) : PdfInspection;
}

public static class PdfInsertInspector
{
    public static PdfInspection Inspect(byte[] pdf)
    {
        if (pdf is null || pdf.Length < 5 || pdf[0] != (byte)'%' || pdf[1] != (byte)'P' || pdf[2] != (byte)'D' || pdf[3] != (byte)'F')
        {
            return new PdfInspection.Rejected("insert.corrupt", "The file is not a readable PDF.");
        }

        try
        {
            using var stream = new MemoryStream(pdf, writable: false);
            using var document = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
            if (document.SecuritySettings.IsEncrypted)
            {
                return new PdfInspection.Rejected("insert.encrypted", "Encrypted PDFs are rejected. Remove the password and upload the file again.");
            }

            if (document.Pages.Count < 1)
            {
                return new PdfInspection.Rejected("insert.empty", "The PDF has no pages.");
            }

            var pages = new List<InsertPageInfo>();
            foreach (var page in document.Pages)
            {
                pages.Add(new InsertPageInfo(page.Width.Point, page.Height.Point, page.Rotate));
            }

            return new PdfInspection.Ok(pages.Count, pages);
        }
        catch (PdfReaderException exception)
        {
            return Classify(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return Classify(exception.Message);
        }
        catch (Exception)
        {
            return new PdfInspection.Rejected("insert.corrupt", "The PDF could not be opened. It looks corrupt or uses a structure this reader cannot import.");
        }
    }

    public static string Describe(PdfInspection.Rejected rejected, string sha256) =>
        rejected.Message + " SHA-256 " + sha256 + ".";

    private static PdfInspection.Rejected Classify(string? message)
    {
        var text = message ?? "";
        if (text.Contains("password", StringComparison.OrdinalIgnoreCase) || text.Contains("encrypt", StringComparison.OrdinalIgnoreCase))
        {
            return new PdfInspection.Rejected("insert.encrypted", "Encrypted PDFs are rejected. Remove the password and upload the file again.");
        }

        return new PdfInspection.Rejected("insert.corrupt", "The PDF could not be opened. It looks corrupt or uses a structure this reader cannot import.");
    }
}

public static class DemoInsertPdfs
{
    public const string ChromatogramFile = "chromatogram.pdf";
    public const string InstrumentLogFile = "instrument-log.pdf";
    public const string AppendixFile = "method-appendix.pdf";

    public static IReadOnlyDictionary<string, byte[]> Create(byte[] font)
    {
        ReportFonts.Ensure(font);
        return new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            [ChromatogramFile] = Chromatogram(),
            [InstrumentLogFile] = InstrumentLog(),
            [AppendixFile] = Appendix()
        };
    }

    public static byte[] Encrypted(byte[] font)
    {
        ReportFonts.Ensure(font);
        var document = new PdfSharp.Pdf.PdfDocument();
        document.SecuritySettings.UserPassword = "secret";
        document.SecuritySettings.OwnerPassword = "secret-owner";
        var page = document.AddPage();
        using (var gfx = PdfSharp.Drawing.XGraphics.FromPdfPage(page))
        {
            gfx.DrawString(
                "hidden",
                new PdfSharp.Drawing.XFont("Liberation Sans", 12, PdfSharp.Drawing.XFontStyleEx.Regular),
                PdfSharp.Drawing.XBrushes.Black,
                40,
                40);
        }

        return Save(document);
    }

    public static void Write(string directory, byte[] font)
    {
        Directory.CreateDirectory(directory);
        foreach (var (name, bytes) in Create(font))
        {
            File.WriteAllBytes(Path.Combine(directory, name), bytes);
        }
    }

    private static byte[] Chromatogram()
    {
        var document = NewDocument("Synthetic chromatogram");
        DrawPortrait(document, "Synthetic chromatogram", "CHROMATOGRAM-A1", "Sheet 1 of 2", "Peak table", drawTrace: true);
        DrawPortrait(document, "Synthetic chromatogram", "CHROMATOGRAM-A2", "Sheet 2 of 2", "Peak table", drawTrace: true);
        return Save(document);
    }

    private static byte[] InstrumentLog()
    {
        var document = NewDocument("Synthetic instrument log");
        for (var page = 1; page <= 5; page++)
        {
            var landscape = page == 3;
            DrawPage(
                document,
                landscape,
                "Synthetic instrument log",
                "INSTRUMENT-LOG-" + page.ToString(CultureInfo.InvariantCulture),
                "Log sheet " + page.ToString(CultureInfo.InvariantCulture) + " of 5",
                landscape ? "This page is landscape." : "Portrait log page.",
                drawTrace: false);
        }

        return Save(document);
    }

    private static byte[] Appendix()
    {
        var document = NewDocument("Synthetic method supplement");
        for (var page = 1; page <= 3; page++)
        {
            DrawPortrait(
                document,
                "Synthetic method supplement",
                "APPENDIX-BODY-" + page.ToString(CultureInfo.InvariantCulture),
                "Supplement sheet " + page.ToString(CultureInfo.InvariantCulture),
                "Fictional method notes for the demonstration report.",
                drawTrace: false);
        }

        return Save(document);
    }

    private static PdfSharp.Pdf.PdfDocument NewDocument(string title)
    {
        var document = new PdfSharp.Pdf.PdfDocument();
        document.Info.Title = title;
        document.Info.Author = "Northline Demonstration Bench";
        document.Info.Subject = "Synthetic insert. Not a laboratory record.";
        document.Info.CreationDate = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        document.Info.ModificationDate = document.Info.CreationDate;
        return document;
    }

    private static void DrawPortrait(PdfSharp.Pdf.PdfDocument document, string title, string marker, string sheet, string footerLine, bool drawTrace) =>
        DrawPage(document, landscape: false, title, marker, sheet, footerLine, drawTrace);

    private static void DrawPage(PdfSharp.Pdf.PdfDocument document, bool landscape, string title, string marker, string sheet, string body, bool drawTrace)
    {
        var page = document.AddPage();
        page.Width = PdfSharp.Drawing.XUnit.FromMillimeter(landscape ? 297 : 210);
        page.Height = PdfSharp.Drawing.XUnit.FromMillimeter(landscape ? 210 : 297);
        using var gfx = PdfSharp.Drawing.XGraphics.FromPdfPage(page);
        var font = new PdfSharp.Drawing.XFont("Liberation Sans", 12, PdfSharp.Drawing.XFontStyleEx.Regular);
        var small = new PdfSharp.Drawing.XFont("Liberation Sans", 10, PdfSharp.Drawing.XFontStyleEx.Regular);
        var brush = PdfSharp.Drawing.XBrushes.Black;
        gfx.DrawString("SYNTHETIC INSERT / NOT A LABORATORY RECORD", small, brush, new PdfSharp.Drawing.XRect(36, 28, page.Width.Point - 72, 18), PdfSharp.Drawing.XStringFormats.TopLeft);
        gfx.DrawString(title, font, brush, new PdfSharp.Drawing.XRect(36, 52, page.Width.Point - 72, 20), PdfSharp.Drawing.XStringFormats.TopLeft);
        gfx.DrawString(marker, font, brush, new PdfSharp.Drawing.XRect(36, 80, page.Width.Point - 72, 20), PdfSharp.Drawing.XStringFormats.TopLeft);
        gfx.DrawString(sheet, small, brush, new PdfSharp.Drawing.XRect(36, 108, page.Width.Point - 72, 16), PdfSharp.Drawing.XStringFormats.TopLeft);
        gfx.DrawString(body, small, brush, new PdfSharp.Drawing.XRect(36, 136, page.Width.Point - 72, 18), PdfSharp.Drawing.XStringFormats.TopLeft);
        if (drawTrace)
        {
            var pen = new PdfSharp.Drawing.XPen(PdfSharp.Drawing.XColors.DarkSlateGray, 1.2);
            gfx.DrawRectangle(pen, 36, 170, page.Width.Point - 72, 80);
            gfx.DrawLine(pen, 48, 230, 90, 190);
            gfx.DrawLine(pen, 90, 190, 140, 220);
            gfx.DrawLine(pen, 140, 220, 210, 180);
        }

        var baseline = page.Height.Point - 40;
        gfx.DrawString("Trace baseline", small, brush, new PdfSharp.Drawing.XRect(36, baseline - 12, page.Width.Point - 72, 14), PdfSharp.Drawing.XStringFormats.TopLeft);
    }

    private static byte[] Save(PdfSharp.Pdf.PdfDocument document)
    {
        using var stream = new MemoryStream();
        document.Save(stream, false);
        document.Dispose();
        return stream.ToArray();
    }
}
