using PdfSharp.Fonts;

namespace LabReport.Core.Pdf;

public static class ReportFonts
{
    private static readonly object Gate = new();
    private static FontResolver? _resolver;

    public static void Ensure(byte[] regular)
    {
        ArgumentNullException.ThrowIfNull(regular);
        lock (Gate)
        {
            if (_resolver is not null)
            {
                return;
            }

            _resolver = new FontResolver(regular);
            GlobalFontSettings.FontResolver = _resolver;
        }
    }

    private sealed class FontResolver(byte[] regular) : IFontResolver
    {
        public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic) =>
            new("liberation-regular");

        public byte[] GetFont(string faceName) => regular;
    }
}
