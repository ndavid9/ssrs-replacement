using System.Text.Json;
using LabReport.Core;
using LabReport.Core.Services;

namespace LabReport.Web.Services;

public sealed class FeedSettingsStore
{
    private readonly AppPaths _paths;
    private readonly object _gate = new();

    public FeedSettingsStore(AppPaths paths) => _paths = paths;

    public IReadOnlyList<string> Enabled()
    {
        var path = Path.Combine(_paths.StorageRoot, "feed-settings.json");
        if (!File.Exists(path))
        {
            return [FeedContracts.ReportSnapshotV1];
        }

        var names = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(path), JsonDefaults.Options) ?? [];
        return names.Where(FeedContracts.IsSafeContractName).Where(name => FeedContracts.TryGet(name, out _)).Distinct().ToList();
    }

    public void Save(IEnumerable<string> names)
    {
        var clean = names.Where(FeedContracts.IsSafeContractName).Where(name => FeedContracts.TryGet(name, out _)).Distinct().ToList();
        if (clean.Count == 0)
        {
            throw new InvalidOperationException("At least one approved feed contract must stay enabled.");
        }

        lock (_gate)
        {
            var path = Path.Combine(_paths.StorageRoot, "feed-settings.json");
            File.WriteAllText(path, JsonSerializer.Serialize(clean, JsonDefaults.Options));
        }
    }
}
