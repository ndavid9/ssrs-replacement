using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace LabReport.Core.Pdf;

public readonly record struct GlyphHit(int PageIndex, double Left, double Bottom, double Right, double Top)
{
    public double Width => Right - Left;
    public double Height => Top - Bottom;

    public bool Overlaps(GlyphHit other) =>
        Left < other.Right && Right > other.Left && Bottom < other.Top && Top > other.Bottom;
}

public static class PdfTextLocator
{
    public static IReadOnlyList<string> PageTexts(byte[] pdf)
    {
        using var document = Open(pdf);
        return document.GetPages().Select(page => page.Text ?? "").ToList();
    }

    public static IReadOnlyList<(double Width, double Height)> PageSizes(byte[] pdf)
    {
        using var document = Open(pdf);
        return document.GetPages().Select(page => (page.Width, page.Height)).ToList();
    }

    public static IReadOnlyList<GlyphHit> Find(byte[] pdf, string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return [];
        }

        using var document = Open(pdf);
        var hits = new List<GlyphHit>();
        foreach (var page in document.GetPages())
        {
            var letters = page.Letters.Where(letter => !string.IsNullOrWhiteSpace(letter.Value)).ToList();
            var text = string.Concat(letters.Select(letter => letter.Value));
            var compact = string.Concat(token.Where(ch => !char.IsWhiteSpace(ch)));
            var start = 0;
            while (compact.Length > 0 && start <= text.Length - compact.Length)
            {
                var index = text.IndexOf(compact, start, StringComparison.Ordinal);
                if (index < 0)
                {
                    break;
                }

                var (first, last) = Map(letters, index, compact.Length);
                hits.Add(Union(page.Number - 1, letters.GetRange(first, last - first + 1)));
                start = index + compact.Length;
            }
        }

        return hits;
    }

    private static PdfDocument Open(byte[] pdf) => PdfDocument.Open(pdf, new ParsingOptions { UseLenientParsing = true });

    private static (int First, int Last) Map(IReadOnlyList<Letter> letters, int charIndex, int charCount)
    {
        var seen = 0;
        var first = -1;
        var last = -1;
        for (var index = 0; index < letters.Count; index++)
        {
            var length = letters[index].Value.Length;
            if (length == 0)
            {
                continue;
            }

            if (first < 0 && seen + length > charIndex)
            {
                first = index;
            }

            if (seen + length >= charIndex + charCount)
            {
                last = index;
                break;
            }

            seen += length;
        }

        if (first < 0 || last < first)
        {
            throw new InvalidOperationException("The matched PDF text could not be mapped back to glyphs.");
        }

        return (first, last);
    }

    private static GlyphHit Union(int pageIndex, IReadOnlyList<Letter> letters)
    {
        var left = letters.Min(letter => letter.GlyphRectangle.Left);
        var bottom = letters.Min(letter => letter.GlyphRectangle.Bottom);
        var right = letters.Max(letter => letter.GlyphRectangle.Right);
        var top = letters.Max(letter => letter.GlyphRectangle.Top);
        return new GlyphHit(pageIndex, left, bottom, right, top);
    }
}
