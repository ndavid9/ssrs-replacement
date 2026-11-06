using System.Text;
using LabReport.Core.Models;
using LabReport.Core.Rendering;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Playwright;

namespace LabReport.Web.Services;

public sealed class SignatoryAssets
{
    private static readonly Dictionary<string, string> Files = new(StringComparer.Ordinal)
    {
        ["demo-mark"] = "demo-mark.svg"
    };

    private readonly string _root;

    public SignatoryAssets(IWebHostEnvironment environment) =>
        _root = Path.Combine(environment.ContentRootPath, "Assets", "signatories");

    public bool IsApproved(string? key) => key is not null && Files.ContainsKey(key);

    public string? DataUri(string? key)
    {
        if (!IsApproved(key))
        {
            return null;
        }

        var path = Path.Combine(_root, Files[key!]);
        var svg = File.ReadAllText(path);
        var scan = svg.Replace("http://www.w3.org/2000/svg", "", StringComparison.Ordinal);
        if (scan.Contains("<script", StringComparison.OrdinalIgnoreCase)
            || scan.Contains("foreignObject", StringComparison.OrdinalIgnoreCase)
            || scan.Contains("http:", StringComparison.OrdinalIgnoreCase)
            || scan.Contains("https:", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Signatory asset failed the allowlist scan.");
        }

        return "data:image/svg+xml;base64," + Convert.ToBase64String(Encoding.UTF8.GetBytes(svg));
    }
}

public sealed class HtmlDocumentComposer
{
    private readonly string _css;

    public HtmlDocumentComposer(IWebHostEnvironment environment)
    {
        var cssPath = Path.Combine(environment.ContentRootPath, "ReportStyles", "report.css");
        var fontCss = BuildFontCss(Path.Combine(environment.ContentRootPath, "Assets", "fonts"));
        _css = fontCss + "\n" + File.ReadAllText(cssPath);
    }

    public string Wrap(string fragment, RenderPlan plan)
    {
        var title = System.Net.WebUtility.HtmlEncode(plan.Snapshot.ReportNumber);
        return "<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\" />"
            + "<title>" + title + "</title><style>" + _css + "</style></head><body>"
            + fragment + "</body></html>";
    }

    private static string BuildFontCss(string fontRoot)
    {
        var builder = new StringBuilder();
        AppendFace(builder, fontRoot, "LiberationSans-Regular.ttf", 400, "normal");
        AppendFace(builder, fontRoot, "LiberationSans-Bold.ttf", 700, "normal");
        AppendFace(builder, fontRoot, "LiberationSans-Italic.ttf", 400, "italic");
        return builder.ToString();
    }

    private static void AppendFace(StringBuilder builder, string root, string file, int weight, string style)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, file));
        builder.Append("@font-face{font-family:\"Liberation Sans\";src:url(data:font/ttf;base64,");
        builder.Append(Convert.ToBase64String(bytes));
        builder.Append(") format(\"truetype\");font-weight:");
        builder.Append(weight);
        builder.Append(";font-style:");
        builder.Append(style);
        builder.Append(";font-display:block;}");
    }
}

public sealed class RazorReportRenderer
{
    private readonly IRazorViewEngine _engine;
    private readonly ITempDataProvider _tempData;
    private readonly IServiceScopeFactory _scopes;

    public RazorReportRenderer(IRazorViewEngine engine, ITempDataProvider tempData, IServiceScopeFactory scopes)
    {
        _engine = engine;
        _tempData = tempData;
        _scopes = scopes;
    }

    public async Task<string> RenderFragmentAsync(RenderPlan plan, CancellationToken cancellationToken)
    {
        using var scope = _scopes.CreateScope();
        var http = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        var action = new ActionContext(http, new RouteData(), new ActionDescriptor());
        action.RouteData.Values["controller"] = "Report";
        var view = _engine.FindView(action, "AnalyticalReport", isMainPage: false);
        if (!view.Success)
        {
            throw new InvalidOperationException("Report view was not found. " + string.Join("; ", view.SearchedLocations));
        }

        await using var writer = new StringWriter();
        var viewData = new ViewDataDictionary<RenderPlan>(new EmptyModelMetadataProvider(), new ModelStateDictionary())
        {
            Model = plan
        };
        var context = new ViewContext(action, view.View, viewData, new TempDataDictionary(http, _tempData), writer, new HtmlHelperOptions());
        await view.View.RenderAsync(context);
        cancellationToken.ThrowIfCancellationRequested();
        return writer.ToString();
    }
}

public sealed class PlaywrightPdfRenderer : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public async Task<byte[]> RenderAsync(string html, RenderPlan plan, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var browser = await BrowserAsync();
            await using var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                Locale = "en-US",
                TimezoneId = "UTC"
            });
            await context.RouteAsync("**/*", async route =>
            {
                var url = route.Request.Url;
                if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
                    || url.StartsWith("about:", StringComparison.OrdinalIgnoreCase)
                    || url.StartsWith("blob:", StringComparison.OrdinalIgnoreCase))
                {
                    await route.ContinueAsync();
                    return;
                }

                await route.AbortAsync();
            });
            var page = await context.NewPageAsync();
            page.SetDefaultTimeout(60_000);
            await page.SetContentAsync(html, new PageSetContentOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 60_000
            });
            var fontsReady = await page.EvaluateAsync<bool>("() => document.fonts.ready.then(() => document.fonts.check('16px \"Liberation Sans\"'))");
            if (!fontsReady)
            {
                throw new InvalidOperationException("Liberation Sans did not become ready. The PDF would not be reproducible.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            var reportNumber = System.Net.WebUtility.HtmlEncode(plan.Snapshot.ReportNumber);
            var templateLabel = System.Net.WebUtility.HtmlEncode(plan.Template.TemplateId + " v" + plan.Template.Version.ToString(System.Globalization.CultureInfo.InvariantCulture));
            var header = "<div style=\"font-size:9px;width:100%;font-family:sans-serif;padding:0 4px;color:#7c2d12;\">"
                + "<div style=\"border:1px solid #c2410c;background:#fff7ed;padding:2px 6px;font-weight:700;\">SYNTHETIC DATA / NOT A VALID LABORATORY CERTIFICATE</div>"
                + "<div style=\"display:flex;justify-content:space-between;margin-top:2px;color:#1c2430;\"><span>Northline Demonstration Bench · "
                + reportNumber + "</span><span>" + templateLabel + "</span></div></div>";
            var footer = "<div style=\"font-size:9px;width:100%;font-family:sans-serif;padding:0 4px;color:#1c2430;display:flex;justify-content:space-between;\">"
                + "<span>Synthetic demo. No accreditation. No cryptographic signature.</span>"
                + "<span>Page <span class=\"pageNumber\"></span> of <span class=\"totalPages\"></span></span></div>";
            var bytes = await page.PdfAsync(new PagePdfOptions
            {
                Format = plan.Template.Paper == "Letter" ? "Letter" : "A4",
                Landscape = plan.Template.Orientation == "landscape",
                PrintBackground = true,
                DisplayHeaderFooter = true,
                HeaderTemplate = header,
                FooterTemplate = footer,
                PreferCSSPageSize = false,
                Margin = new Margin
                {
                    Top = plan.Template.MarginTopMm + "mm",
                    Bottom = plan.Template.MarginBottomMm + "mm",
                    Left = plan.Template.MarginSideMm + "mm",
                    Right = plan.Template.MarginSideMm + "mm"
                }
            });
            if (bytes.Length < 5 || bytes[0] != (byte)'%' || bytes[1] != (byte)'P')
            {
                throw new InvalidOperationException("Chromium did not return a PDF.");
            }

            return bytes;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<IBrowser> BrowserAsync()
    {
        if (_browser is not null)
        {
            return _browser;
        }

        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Args = ["--disable-dev-shm-usage", "--no-first-run", "--no-default-browser-check"]
        });
        return _browser;
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.DisposeAsync();
        }

        _playwright?.Dispose();
    }
}

public sealed class ReportDocumentRenderer
{
    private readonly RazorReportRenderer _razor;
    private readonly HtmlDocumentComposer _composer;
    private readonly PlaywrightPdfRenderer _pdf;
    private readonly SignatoryAssets _assets;

    public ReportDocumentRenderer(RazorReportRenderer razor, HtmlDocumentComposer composer, PlaywrightPdfRenderer pdf, SignatoryAssets assets)
    {
        _razor = razor;
        _composer = composer;
        _pdf = pdf;
        _assets = assets;
    }

    public async Task<string> RenderHtmlAsync(RenderPlan plan, CancellationToken cancellationToken)
    {
        if (!plan.IsValid)
        {
            throw new InvalidOperationException("Refusing to render a plan that failed validation.");
        }

        var withAsset = plan with { SignatoryDataUri = _assets.DataUri(plan.Signatory?.AssetKey) };
        var fragment = await _razor.RenderFragmentAsync(withAsset, cancellationToken);
        return _composer.Wrap(fragment, withAsset);
    }

    public async Task<byte[]> RenderPdfAsync(RenderPlan plan, CancellationToken cancellationToken)
    {
        var html = await RenderHtmlAsync(plan, cancellationToken);
        return await _pdf.RenderAsync(html, plan, cancellationToken);
    }
}

public sealed class CatalogEditor
{
    private readonly FileCatalogStore _store;
    private readonly JsonReportFeed _feed;
    private readonly DemoAuthorizer _authorizer;

    public CatalogEditor(FileCatalogStore store, JsonReportFeed feed, DemoAuthorizer authorizer)
    {
        _store = store;
        _feed = feed;
        _authorizer = authorizer;
    }

    public void AddTextVersion(string key, TextScope scope, TextCategory category, string body, DateOnly effectiveFrom)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 80 || key.Any(ch => !char.IsLetterOrDigit(ch) && ch is not '.' and not '-'))
        {
            throw new InvalidOperationException("Text keys are limited to letters, digits, dots, and hyphens.");
        }

        if (body.Contains('<') || body.Contains('>'))
        {
            throw new InvalidOperationException("Raw HTML is disabled. Enter plain text.");
        }

        var entries = _store.Texts().ToList();
        var existing = entries.Where(entry => entry.Key == key).ToList();
        if (existing.Count > 0 && existing.Any(entry => entry.Scope != scope || entry.Category != category))
        {
            throw new InvalidOperationException("An existing key cannot change scope or category.");
        }

        var version = "admin-" + (existing.Count + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
        entries.Add(new TextEntry
        {
            Key = key.Trim(),
            Language = "en",
            VersionLabel = version,
            Scope = scope,
            Category = category,
            EffectiveFrom = effectiveFrom,
            EffectiveTo = null,
            ApprovalStatus = ApprovalStatus.Approved,
            Body = body.Trim()
        });
        _store.SaveTexts(entries);
    }

    public void AddFictionalLimit(string analyteCode, string jurisdiction, string matrix, string commodity, string unit, decimal limitValue, DateOnly effectiveFrom, string sourceName, bool confirmed)
    {
        if (!confirmed)
        {
            throw new InvalidOperationException("Confirm that the limit is fictional before saving it.");
        }

        var known = KnownAnalytes();
        if (!known.Contains(analyteCode))
        {
            throw new InvalidOperationException("Analyte code must already exist on a demo feed.");
        }

        var limits = _store.Mrls().ToList();
        var id = "mrl-admin-" + (limits.Count(limit => limit.MrlId.StartsWith("mrl-admin-", StringComparison.Ordinal)) + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
        limits.Add(new MrlLimit
        {
            MrlId = id,
            AnalyteCode = analyteCode,
            Jurisdiction = jurisdiction.Trim(),
            Matrix = matrix.Trim(),
            Commodity = commodity.Trim(),
            Unit = unit.Trim(),
            LimitValue = limitValue,
            EffectiveFrom = effectiveFrom,
            EffectiveTo = null,
            SourceName = sourceName.Trim(),
            VersionLabel = "admin",
            Fictional = true
        });
        _store.SaveMrls(limits);
    }

    public IReadOnlyList<string> KnownAnalyteCodes() => KnownAnalytes().OrderBy(code => code, StringComparer.Ordinal).ToList();

    private HashSet<string> KnownAnalytes()
    {
        var reports = _feed.ListAsync(_authorizer.TenantId, _authorizer.PrincipalId, CancellationToken.None).GetAwaiter().GetResult();
        var codes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var report in reports)
        {
            var snapshot = _feed.GetAsync(_authorizer.TenantId, report.ReportId, _authorizer.PrincipalId, CancellationToken.None).GetAwaiter().GetResult();
            if (snapshot is null)
            {
                continue;
            }

            foreach (var result in snapshot.Samples.SelectMany(sample => sample.Results))
            {
                codes.Add(result.AnalyteCode);
            }
        }

        return codes;
    }
}
