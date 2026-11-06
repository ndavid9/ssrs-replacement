using System.Data;
using System.Text.Json;
using LabReport.Core;
using LabReport.Core.Models;
using LabReport.Core.Services;
using Microsoft.Data.SqlClient;

namespace LabReport.Web.Services;

public sealed record ReportListItem(string ReportId, string ReportNumber, string Title, DateOnly ReportDate);

public interface IReportFeed
{
    string Name { get; }
    Task<IReadOnlyList<ReportListItem>> ListAsync(string tenantId, string principalId, CancellationToken cancellationToken);
    Task<ReportSnapshot?> GetAsync(string tenantId, string reportId, string principalId, CancellationToken cancellationToken);
}

public sealed class JsonReportFeed : IReportFeed
{
    private readonly AppPaths _paths;

    public JsonReportFeed(AppPaths paths) => _paths = paths;

    public string Name => "Json";

    public Task<IReadOnlyList<ReportListItem>> ListAsync(string tenantId, string principalId, CancellationToken cancellationToken)
    {
        if (!PathSafety.IsSafeId(tenantId))
        {
            return Task.FromResult<IReadOnlyList<ReportListItem>>([]);
        }

        var directory = PathSafety.CombineUnderRoot(_paths.FeedRoot, FeedContracts.ReportSnapshotV1, tenantId);
        if (!Directory.Exists(directory))
        {
            return Task.FromResult<IReadOnlyList<ReportListItem>>([]);
        }

        var items = new List<ReportListItem>();
        foreach (var file in Directory.EnumerateFiles(directory, "*.json").OrderBy(path => path, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = JsonSerializer.Deserialize<ReportSnapshot>(File.ReadAllText(file), JsonDefaults.Options);
            if (snapshot is null || !string.Equals(snapshot.TenantId, tenantId, StringComparison.Ordinal))
            {
                continue;
            }

            items.Add(new ReportListItem(snapshot.ReportId, snapshot.ReportNumber, snapshot.Title, snapshot.ReportDate));
        }

        return Task.FromResult<IReadOnlyList<ReportListItem>>(items);
    }

    public Task<ReportSnapshot?> GetAsync(string tenantId, string reportId, string principalId, CancellationToken cancellationToken)
    {
        if (!PathSafety.IsSafeId(tenantId) || !PathSafety.IsSafeId(reportId))
        {
            return Task.FromResult<ReportSnapshot?>(null);
        }

        var path = PathSafety.CombineUnderRoot(_paths.FeedRoot, FeedContracts.ReportSnapshotV1, tenantId, reportId + ".json");
        if (!File.Exists(path))
        {
            return Task.FromResult<ReportSnapshot?>(null);
        }

        var snapshot = JsonSerializer.Deserialize<ReportSnapshot>(File.ReadAllText(path), JsonDefaults.Options);
        if (snapshot is null || !string.Equals(snapshot.TenantId, tenantId, StringComparison.Ordinal) || !string.Equals(snapshot.ReportId, reportId, StringComparison.Ordinal))
        {
            return Task.FromResult<ReportSnapshot?>(null);
        }

        return Task.FromResult<ReportSnapshot?>(SnapshotCanonicalizer.Canonicalize(snapshot));
    }
}

public sealed class SqlReportFeed : IReportFeed
{
    public const string SnapshotCommand = "rpt.usp_ExportReportSnapshot";
    public const string ListCommand = "rpt.usp_ListReports";
    public const string MrlCommand = "rpt.usp_ExportMrlCatalog";
    public const string TextCommand = "rpt.usp_ExportTextCatalog";

    private readonly string _connectionString;

    public SqlReportFeed(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("SQL provider is selected but ConnectionStrings:ReportDb is empty.");
        }

        _connectionString = connectionString;
    }

    public string Name => "Sql";

    public async Task<IReadOnlyList<ReportListItem>> ListAsync(string tenantId, string principalId, CancellationToken cancellationToken)
    {
        RequireIds(tenantId, principalId);
        var items = new List<ReportListItem>();
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(ListCommand, connection) { CommandType = CommandType.StoredProcedure };
        command.Parameters.Add(new SqlParameter("@TenantId", SqlDbType.NVarChar, 64) { Value = tenantId });
        command.Parameters.Add(new SqlParameter("@PrincipalId", SqlDbType.NVarChar, 64) { Value = principalId });
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new ReportListItem(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                DateOnly.FromDateTime(reader.GetDateTime(3))));
        }

        return items;
    }

    public async Task<ReportSnapshot?> GetAsync(string tenantId, string reportId, string principalId, CancellationToken cancellationToken)
    {
        RequireIds(tenantId, reportId, principalId);
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(SnapshotCommand, connection) { CommandType = CommandType.StoredProcedure };
        command.Parameters.Add(new SqlParameter("@TenantId", SqlDbType.NVarChar, 64) { Value = tenantId });
        command.Parameters.Add(new SqlParameter("@ReportId", SqlDbType.NVarChar, 64) { Value = reportId });
        command.Parameters.Add(new SqlParameter("@PrincipalId", SqlDbType.NVarChar, 64) { Value = principalId });
        var payload = (string?)await command.ExecuteScalarAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        var snapshot = JsonSerializer.Deserialize<ReportSnapshot>(payload, JsonDefaults.Options)
            ?? throw new InvalidOperationException("The SQL snapshot procedure returned JSON that did not match the contract.");
        if (!string.Equals(snapshot.TenantId, tenantId, StringComparison.Ordinal) || !string.Equals(snapshot.ReportId, reportId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The SQL snapshot did not match the requested tenant and report.");
        }

        return SnapshotCanonicalizer.Canonicalize(snapshot);
    }

    public async Task<IReadOnlyList<MrlLimit>> MrlsAsync(CancellationToken cancellationToken)
    {
        var payload = await ScalarAsync(MrlCommand, cancellationToken);
        var file = JsonSerializer.Deserialize<MrlCatalogFile>(payload, JsonDefaults.Options)
            ?? throw new InvalidOperationException("The MRL catalog procedure returned an empty document.");
        return file.Limits;
    }

    public async Task<IReadOnlyList<TextEntry>> TextsAsync(CancellationToken cancellationToken)
    {
        var payload = await ScalarAsync(TextCommand, cancellationToken);
        var file = JsonSerializer.Deserialize<TextCatalogFile>(payload, JsonDefaults.Options)
            ?? throw new InvalidOperationException("The text catalog procedure returned an empty document.");
        return file.Entries;
    }

    private async Task<string> ScalarAsync(string commandText, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(commandText, connection) { CommandType = CommandType.StoredProcedure };
        var payload = (string?)await command.ExecuteScalarAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(payload))
        {
            throw new InvalidOperationException($"Procedure {commandText} returned no JSON.");
        }

        return payload;
    }

    private static void RequireIds(params string[] ids)
    {
        if (ids.Any(id => !PathSafety.IsSafeId(id)))
        {
            throw new InvalidOperationException("An identifier was rejected before it reached SQL.");
        }
    }
}

public sealed class SqlCatalogSource : ICatalogSource
{
    private readonly SqlReportFeed _sql;
    private readonly FileCatalogStore _files;
    private IReadOnlyList<TextEntry>? _texts;
    private IReadOnlyList<MrlLimit>? _mrls;

    public SqlCatalogSource(SqlReportFeed sql, FileCatalogStore files)
    {
        _sql = sql;
        _files = files;
    }

    public bool CanEdit => false;

    public IReadOnlyList<TextEntry> Texts() => _texts ??= _sql.TextsAsync(CancellationToken.None).GetAwaiter().GetResult();

    public IReadOnlyList<MrlLimit> Mrls() => _mrls ??= _sql.MrlsAsync(CancellationToken.None).GetAwaiter().GetResult();

    public IReadOnlyList<SignatoryBlock> Signatories() => _files.Signatories();

    public SignatoryBlock? Signatory(string? id) => _files.Signatory(id);
}

public sealed class DemoAuthorizer
{
    public DemoAuthorizer(IConfiguration configuration)
    {
        TenantId = configuration["Demo:TenantId"] ?? "demo-tenant";
        PrincipalId = configuration["Demo:PrincipalId"] ?? "demo-reader";
    }

    public string TenantId { get; }
    public string PrincipalId { get; }

    public bool Allow(string tenantId, string principalId) =>
        string.Equals(tenantId, TenantId, StringComparison.Ordinal)
        && string.Equals(principalId, PrincipalId, StringComparison.Ordinal);
}
