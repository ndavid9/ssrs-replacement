namespace LabReport.Core.Models;

public enum ResultKind
{
    Numeric,
    BelowLoq,
    NotDetected,
    NotTested,
    Missing
}

public enum TextScope
{
    Report,
    Sample,
    Result,
    Method,
    Section
}

public enum TextCategory
{
    Disclaimer,
    Comment,
    Conclusion,
    ResultNote,
    MethodNote,
    SectionIntro
}

public enum ApprovalStatus
{
    Approved,
    Draft
}

public enum SectionPlacement
{
    Report,
    Sample
}

public enum JobStatus
{
    Queued,
    Rendering,
    Succeeded,
    Failed,
    FailedValidation,
    Cancelled
}

public enum PdfInsertMode
{
    Attachment,
    Appendix
}

public sealed record ReportSnapshot
{
    public required string SchemaVersion { get; init; }
    public required string TenantId { get; init; }
    public required string ReportId { get; init; }
    public required string ReportNumber { get; init; }
    public required string Title { get; init; }
    public required DateOnly ReportDate { get; init; }
    public required string Matrix { get; init; }
    public required string Commodity { get; init; }
    public required string ClientName { get; init; }
    public string? ClientReference { get; init; }
    public string? Origin { get; init; }
    public string? OrderedBy { get; init; }
    public string? SubmittedBy { get; init; }
    public required string SourceFeed { get; init; }
    public required string SourceRevision { get; init; }
    public required IReadOnlyList<SampleSnapshot> Samples { get; init; }
    public required IReadOnlyList<MethodReference> Methods { get; init; }
    public required IReadOnlyList<TextBinding> TextBindings { get; init; }
}

public sealed record SampleSnapshot
{
    public required string SampleId { get; init; }
    public required string SampleCode { get; init; }
    public required int Ordinal { get; init; }
    public required string Description { get; init; }
    public required string Matrix { get; init; }
    public required string Commodity { get; init; }
    public DateOnly? SamplingDate { get; init; }
    public DateOnly? ReceivedDate { get; init; }
    public string? ReceptionTemperature { get; init; }
    public bool BabyFood { get; init; }
    public bool Organic { get; init; }
    public required IReadOnlyList<ResultSnapshot> Results { get; init; }
}

public sealed record ResultSnapshot
{
    public required string ResultId { get; init; }
    public required string SampleId { get; init; }
    public required string AnalyteCode { get; init; }
    public required string AnalyteName { get; init; }
    public required string AnalyteGroup { get; init; }
    public required int Ordinal { get; init; }
    public required ResultKind Kind { get; init; }
    public decimal? NumericValue { get; init; }
    public string? Qualifier { get; init; }
    public decimal? Lod { get; init; }
    public decimal? Loq { get; init; }
    public string? Unit { get; init; }
    public string? MethodCode { get; init; }
}

public sealed record MethodReference
{
    public required string MethodCode { get; init; }
    public required string MethodName { get; init; }
    public required string Summary { get; init; }
    public required string ReferenceCitation { get; init; }
    public required string VersionLabel { get; init; }
}

public sealed record TextBinding
{
    public required string BindingId { get; init; }
    public required string TextKey { get; init; }
    public required TextScope Scope { get; init; }
    public string? SampleId { get; init; }
    public string? ResultId { get; init; }
    public string? MethodCode { get; init; }
}

public sealed record ColumnConfig
{
    public required string Key { get; init; }
    public required string Label { get; init; }
    public bool Visible { get; init; } = true;
}

public sealed record SubsectionConfig
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public int Order { get; init; }
    public bool Enabled { get; init; } = true;
    public string? AnalyteGroup { get; init; }
}

public sealed record SectionConfig
{
    public required string InstanceId { get; init; }
    public required string Type { get; init; }
    public required string Title { get; init; }
    public int Order { get; init; }
    public bool Enabled { get; init; } = true;
    public IReadOnlyList<ColumnConfig> Columns { get; init; } = [];
    public IReadOnlyList<SubsectionConfig> Subsections { get; init; } = [];
    public IReadOnlyList<string> TextKeys { get; init; } = [];
}

public sealed record TemplateDefinition
{
    public required string TemplateId { get; init; }
    public required string Name { get; init; }
    public required int Version { get; init; }
    public required string Language { get; init; }
    public required int SamplesPerPage { get; init; }
    public required int ResultRowsPerChunk { get; init; }
    public required string Paper { get; init; }
    public required string Orientation { get; init; }
    public required int MarginTopMm { get; init; }
    public required int MarginBottomMm { get; init; }
    public required int MarginSideMm { get; init; }
    public required string SignatoryId { get; init; }
    public required string Jurisdiction { get; init; }
    public required DateTimeOffset UpdatedUtc { get; init; }
    public required IReadOnlyList<SectionConfig> Sections { get; init; }
    public IReadOnlyList<PdfInsert> Inserts
    {
        get => _inserts;
        init => _inserts = value ?? [];
    }

    private readonly IReadOnlyList<PdfInsert> _inserts = [];
}

public sealed record PdfInsert
{
    public required string Id { get; init; }
    public required string File { get; init; }
    public required PdfInsertMode Mode { get; init; }
    public required string Slot { get; init; }
    public required string Label { get; init; }
}

public sealed record InsertDigest
{
    public required string Id { get; init; }
    public required string File { get; init; }
    public required string Sha256 { get; init; }
    public int PageCount { get; init; }
    public bool Accepted { get; init; }
}

public sealed record TextEntry
{
    public required string Key { get; init; }
    public required string Language { get; init; }
    public required string VersionLabel { get; init; }
    public required TextScope Scope { get; init; }
    public required TextCategory Category { get; init; }
    public required DateOnly EffectiveFrom { get; init; }
    public DateOnly? EffectiveTo { get; init; }
    public required ApprovalStatus ApprovalStatus { get; init; }
    public required string Body { get; init; }
}

public sealed record TextCatalogFile
{
    public required string CatalogVersion { get; init; }
    public required IReadOnlyList<TextEntry> Entries { get; init; }
}

public sealed record MrlLimit
{
    public required string MrlId { get; init; }
    public required string AnalyteCode { get; init; }
    public required string Jurisdiction { get; init; }
    public required string Matrix { get; init; }
    public required string Commodity { get; init; }
    public required string Unit { get; init; }
    public required decimal LimitValue { get; init; }
    public required DateOnly EffectiveFrom { get; init; }
    public DateOnly? EffectiveTo { get; init; }
    public required string SourceName { get; init; }
    public required string VersionLabel { get; init; }
    public required bool Fictional { get; init; }
}

public sealed record MrlCatalogFile
{
    public required string CatalogVersion { get; init; }
    public required IReadOnlyList<MrlLimit> Limits { get; init; }
}

public sealed record SignatoryBlock
{
    public required string SignatoryId { get; init; }
    public required string DisplayName { get; init; }
    public required string RoleTitle { get; init; }
    public required string Statement { get; init; }
    public required string AssetKey { get; init; }
}

public sealed record SignatoryCatalogFile
{
    public required string CatalogVersion { get; init; }
    public required IReadOnlyList<SignatoryBlock> Signatories { get; init; }
}

public sealed record ValidationIssue(string Code, string Message);

public sealed record JobRecord
{
    public required Guid Id { get; init; }
    public required string TenantId { get; init; }
    public required string ReportId { get; init; }
    public required string TemplateId { get; init; }
    public required int TemplateVersion { get; init; }
    public required string InputHash { get; init; }
    public string? PdfHash { get; init; }
    public required JobStatus Status { get; init; }
    public required int Attempt { get; init; }
    public required DateTimeOffset CreatedUtc { get; init; }
    public DateTimeOffset? FinishedUtc { get; init; }
    public bool CancelRequested { get; init; }
    public string? Error { get; init; }
    public IReadOnlyList<ValidationIssue> Issues { get; init; } = [];
    public string? SigningNote { get; init; }
    public bool CryptographicSignatureApplied { get; init; }
    public Guid? DeduplicatedFrom { get; init; }
    public IReadOnlyList<InsertDigest> InsertDigests
    {
        get => _insertDigests;
        init => _insertDigests = value ?? [];
    }

    private readonly IReadOnlyList<InsertDigest> _insertDigests = [];
}

public sealed record PinnedInput
{
    public required string SchemaVersion { get; init; }
    public required ReportSnapshot Snapshot { get; init; }
    public required TemplateDefinition Template { get; init; }
    public required IReadOnlyList<TextEntry> Texts { get; init; }
    public required IReadOnlyList<MrlLimit> Mrls { get; init; }
    public SignatoryBlock? Signatory { get; init; }
    public required int SamplesPerPage { get; init; }
    public required string Paper { get; init; }
    public required string Orientation { get; init; }
    public IReadOnlyList<InsertDigest> Inserts
    {
        get => _inserts;
        init => _inserts = value ?? [];
    }

    private readonly IReadOnlyList<InsertDigest> _inserts = [];
}

public sealed record FeedField(string Name, string Description, bool Required);

public sealed record FeedContract(
    string Name,
    string DisplayName,
    string ObjectKind,
    string SqlObject,
    IReadOnlyList<string> Parameters,
    IReadOnlyList<FeedField> Fields);
