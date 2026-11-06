using System.Text.Json;
using LabReport.Core;
using LabReport.Core.Models;
using LabReport.Core.Services;

namespace LabReport.Web.Services;

public sealed class AppPaths
{
    public AppPaths(IWebHostEnvironment environment, IConfiguration configuration)
    {
        ContentRoot = environment.ContentRootPath;
        var configured = configuration["Storage:Root"];
        StorageRoot = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(ContentRoot, "App_Data")
            : Path.GetFullPath(configured);
        SeedRoot = Path.Combine(ContentRoot, "Seed");
    }

    public string ContentRoot { get; }
    public string StorageRoot { get; }
    public string SeedRoot { get; }
    public string FeedRoot => Path.Combine(SeedRoot, "feeds");
    public string TemplateRoot => Path.Combine(StorageRoot, "templates");
    public string CatalogRoot => Path.Combine(StorageRoot, "catalogs");
    public string JobRoot => Path.Combine(StorageRoot, "jobs");
    public string PinRoot => Path.Combine(StorageRoot, "pins");
    public string ArtifactRoot => Path.Combine(StorageRoot, "artifacts");
    public string AuditPath => Path.Combine(StorageRoot, "audit.jsonl");
}

public static class SeedBootstrap
{
    public static void Ensure(AppPaths paths)
    {
        Directory.CreateDirectory(paths.StorageRoot);
        Directory.CreateDirectory(paths.TemplateRoot);
        Directory.CreateDirectory(paths.CatalogRoot);
        Directory.CreateDirectory(paths.JobRoot);
        Directory.CreateDirectory(paths.PinRoot);
        Directory.CreateDirectory(paths.ArtifactRoot);
        CopyMissing(Path.Combine(paths.SeedRoot, "templates"), paths.TemplateRoot);
        CopyMissing(Path.Combine(paths.SeedRoot, "catalogs"), paths.CatalogRoot);
    }

    private static void CopyMissing(string source, string destination)
    {
        if (!Directory.Exists(source))
        {
            throw new DirectoryNotFoundException($"Seed folder is missing: {source}");
        }

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            var target = PathSafety.CombineUnderRoot(destination, relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (File.Exists(target))
            {
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }
}

public sealed class FileTemplateStore
{
    private readonly AppPaths _paths;
    private readonly object _gate = new();

    public FileTemplateStore(AppPaths paths) => _paths = paths;

    public IReadOnlyList<TemplateDefinition> List()
    {
        lock (_gate)
        {
            return Directory.EnumerateFiles(_paths.TemplateRoot, "*.json")
                .Select(path => JsonSerializer.Deserialize<TemplateDefinition>(File.ReadAllText(path), JsonDefaults.Options)!)
                .OrderBy(template => template.TemplateId, StringComparer.Ordinal)
                .ToList();
        }
    }

    public TemplateDefinition? Find(string templateId)
    {
        if (!PathSafety.IsSafeId(templateId))
        {
            return null;
        }

        var path = PathSafety.CombineUnderRoot(_paths.TemplateRoot, templateId + ".json");
        if (!File.Exists(path))
        {
            return null;
        }

        return JsonSerializer.Deserialize<TemplateDefinition>(File.ReadAllText(path), JsonDefaults.Options);
    }

    public TemplateDefinition Require(string templateId) =>
        Find(templateId) ?? throw new InvalidOperationException($"Unknown template '{templateId}'.");

    public void Save(TemplateDefinition template)
    {
        if (!PathSafety.IsSafeId(template.TemplateId))
        {
            throw new InvalidOperationException("Template id is not safe.");
        }

        lock (_gate)
        {
            var path = PathSafety.CombineUnderRoot(_paths.TemplateRoot, template.TemplateId + ".json");
            var history = PathSafety.CombineUnderRoot(_paths.TemplateRoot, "history", template.TemplateId, "v" + template.Version.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".json");
            Directory.CreateDirectory(Path.GetDirectoryName(history)!);
            var json = JsonSerializer.Serialize(template, JsonDefaults.Options);
            File.WriteAllText(history, json);
            WriteAtomic(path, json);
        }
    }

    private static void WriteAtomic(string path, string json)
    {
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, json);
        File.Move(temporary, path, overwrite: true);
    }
}

public interface ICatalogSource
{
    IReadOnlyList<TextEntry> Texts();
    IReadOnlyList<MrlLimit> Mrls();
    IReadOnlyList<SignatoryBlock> Signatories();
    SignatoryBlock? Signatory(string? id);
    bool CanEdit { get; }
}

public sealed class FileCatalogStore : ICatalogSource
{
    private readonly AppPaths _paths;
    private readonly object _gate = new();

    public FileCatalogStore(AppPaths paths) => _paths = paths;

    public bool CanEdit => true;

    public IReadOnlyList<TextEntry> Texts() => Read<TextCatalogFile>("texts.json").Entries;

    public IReadOnlyList<MrlLimit> Mrls() => Read<MrlCatalogFile>("mrl.json").Limits;

    public IReadOnlyList<SignatoryBlock> Signatories() => Read<SignatoryCatalogFile>("signatories.json").Signatories;

    public SignatoryBlock? Signatory(string? id) =>
        Signatories().FirstOrDefault(item => item.SignatoryId == id);

    public void SaveTexts(IReadOnlyList<TextEntry> entries)
    {
        lock (_gate)
        {
            var current = Read<TextCatalogFile>("texts.json");
            Write("texts.json", current with { Entries = entries.OrderBy(entry => entry.Key, StringComparer.Ordinal).ThenBy(entry => entry.VersionLabel, StringComparer.Ordinal).ToList() });
        }
    }

    public void SaveMrls(IReadOnlyList<MrlLimit> limits)
    {
        var issues = MrlCatalogValidator.Validate(limits);
        if (issues.Count > 0)
        {
            throw new InvalidOperationException(string.Join(" ", issues.Select(issue => issue.Message)));
        }

        lock (_gate)
        {
            var current = Read<MrlCatalogFile>("mrl.json");
            Write("mrl.json", current with { Limits = limits.OrderBy(limit => limit.MrlId, StringComparer.Ordinal).ToList() });
        }
    }

    private T Read<T>(string file)
    {
        var path = PathSafety.CombineUnderRoot(_paths.CatalogRoot, file);
        return JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonDefaults.Options)!;
    }

    private void Write<T>(string file, T value)
    {
        var path = PathSafety.CombineUnderRoot(_paths.CatalogRoot, file);
        var json = JsonSerializer.Serialize(value, JsonDefaults.Options);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, json);
        File.Move(temporary, path, overwrite: true);
    }
}
