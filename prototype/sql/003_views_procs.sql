-- Read contract. Parameters only. No dynamic SQL.
-- Result grain view is one row per result. Sample attributes are many-to-one.
-- Do not join rpt.vw_ResultGrain to lab.TextBinding; that would multiply rows.

CREATE OR ALTER VIEW rpt.vw_SampleGrain
AS
SELECT
    s.TenantId,
    s.ReportId,
    s.SampleId,
    s.SampleCode,
    s.Ordinal,
    s.Matrix,
    s.Commodity,
    s.Description
FROM lab.Sample AS s;
GO

CREATE OR ALTER VIEW rpt.vw_ResultGrain
AS
SELECT
    res.TenantId,
    s.ReportId,
    res.SampleId,
    res.ResultId,
    s.Ordinal AS SampleOrdinal,
    res.Ordinal AS ResultOrdinal,
    res.AnalyteCode,
    a.AnalyteName,
    a.AnalyteGroup,
    res.ResultKind,
    res.NumericValue,
    res.Qualifier,
    res.Lod,
    res.Loq,
    res.Unit,
    res.MethodCode
FROM lab.Result AS res
INNER JOIN lab.Sample AS s
    ON s.TenantId = res.TenantId
   AND s.SampleId = res.SampleId
INNER JOIN lab.Analyte AS a
    ON a.AnalyteCode = res.AnalyteCode;
GO

CREATE OR ALTER PROCEDURE rpt.usp_ListReports
    @TenantId nvarchar(64),
    @PrincipalId nvarchar(64)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        r.ReportId,
        r.ReportNumber,
        r.Title,
        r.ReportDate
    FROM lab.Report AS r
    INNER JOIN lab.ReportAccess AS a
        ON a.TenantId = r.TenantId
       AND a.ReportId = r.ReportId
    WHERE r.TenantId = @TenantId
      AND a.PrincipalId = @PrincipalId
      AND a.AccessLevel IN (N'reader', N'admin')
    ORDER BY r.ReportId;
END;
GO

CREATE OR ALTER PROCEDURE rpt.usp_ExportReportSnapshot
    @TenantId nvarchar(64),
    @ReportId nvarchar(64),
    @PrincipalId nvarchar(64)
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (
        SELECT 1
        FROM lab.ReportAccess AS a
        WHERE a.TenantId = @TenantId
          AND a.ReportId = @ReportId
          AND a.PrincipalId = @PrincipalId
          AND a.AccessLevel IN (N'reader', N'admin')
    )
    BEGIN
        RAISERROR(N'Report access denied for the supplied tenant, report, and principal.', 16, 1);
        RETURN;
    END;

    SELECT (
        SELECT
            r.SchemaVersion AS schemaVersion,
            r.TenantId AS tenantId,
            r.ReportId AS reportId,
            r.ReportNumber AS reportNumber,
            r.Title AS title,
            CONVERT(char(10), r.ReportDate, 23) AS reportDate,
            r.Matrix AS matrix,
            r.Commodity AS commodity,
            r.ClientName AS clientName,
            r.ClientReference AS clientReference,
            r.Origin AS origin,
            r.OrderedBy AS orderedBy,
            r.SubmittedBy AS submittedBy,
            r.SourceFeed AS sourceFeed,
            r.SourceRevision AS sourceRevision,
            JSON_QUERY(ISNULL((
                SELECT
                    s.SampleId AS sampleId,
                    s.SampleCode AS sampleCode,
                    s.Ordinal AS ordinal,
                    s.Description AS description,
                    s.Matrix AS matrix,
                    s.Commodity AS commodity,
                    CONVERT(char(10), s.SamplingDate, 23) AS samplingDate,
                    CONVERT(char(10), s.ReceivedDate, 23) AS receivedDate,
                    s.ReceptionTemperature AS receptionTemperature,
                    CAST(s.BabyFood AS bit) AS babyFood,
                    CAST(s.Organic AS bit) AS organic,
                    JSON_QUERY(ISNULL((
                        SELECT
                            res.ResultId AS resultId,
                            res.SampleId AS sampleId,
                            res.AnalyteCode AS analyteCode,
                            an.AnalyteName AS analyteName,
                            an.AnalyteGroup AS analyteGroup,
                            res.Ordinal AS ordinal,
                            res.ResultKind AS kind,
                            res.NumericValue AS numericValue,
                            res.Qualifier AS qualifier,
                            res.Lod AS lod,
                            res.Loq AS loq,
                            res.Unit AS unit,
                            res.MethodCode AS methodCode
                        FROM lab.Result AS res
                        INNER JOIN lab.Analyte AS an
                            ON an.AnalyteCode = res.AnalyteCode
                        WHERE res.TenantId = s.TenantId
                          AND res.SampleId = s.SampleId
                        ORDER BY res.Ordinal, res.AnalyteCode
                        FOR JSON PATH, INCLUDE_NULL_VALUES
                    ), N'[]')) AS results
                FROM lab.Sample AS s
                WHERE s.TenantId = r.TenantId
                  AND s.ReportId = r.ReportId
                ORDER BY s.Ordinal, s.SampleId
                FOR JSON PATH, INCLUDE_NULL_VALUES
            ), N'[]')) AS samples,
            JSON_QUERY(ISNULL((
                SELECT
                    m.MethodCode AS methodCode,
                    m.MethodName AS methodName,
                    m.Summary AS summary,
                    m.ReferenceCitation AS referenceCitation,
                    m.VersionLabel AS versionLabel
                FROM lab.MethodReference AS m
                WHERE EXISTS (
                    SELECT 1
                    FROM lab.Result AS res
                    INNER JOIN lab.Sample AS s
                        ON s.TenantId = res.TenantId
                       AND s.SampleId = res.SampleId
                    WHERE res.MethodCode = m.MethodCode
                      AND s.TenantId = r.TenantId
                      AND s.ReportId = r.ReportId
                )
                ORDER BY m.MethodCode
                FOR JSON PATH, INCLUDE_NULL_VALUES
            ), N'[]')) AS methods,
            JSON_QUERY(ISNULL((
                SELECT
                    b.BindingId AS bindingId,
                    b.TextKey AS textKey,
                    b.Scope AS scope,
                    b.SampleId AS sampleId,
                    b.ResultId AS resultId,
                    b.MethodCode AS methodCode
                FROM lab.TextBinding AS b
                WHERE b.TenantId = r.TenantId
                  AND b.ReportId = r.ReportId
                ORDER BY b.BindingId
                FOR JSON PATH, INCLUDE_NULL_VALUES
            ), N'[]')) AS textBindings
        FROM lab.Report AS r
        WHERE r.TenantId = @TenantId
          AND r.ReportId = @ReportId
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES
    ) AS SnapshotJson;
END;
GO

CREATE OR ALTER PROCEDURE rpt.usp_ExportMrlCatalog
AS
BEGIN
    SET NOCOUNT ON;
    SELECT (
        SELECT
            N'fic-2026.1' AS catalogVersion,
            JSON_QUERY(ISNULL((
                SELECT
                    m.MrlId AS mrlId,
                    m.AnalyteCode AS analyteCode,
                    m.Jurisdiction AS jurisdiction,
                    m.Matrix AS matrix,
                    m.Commodity AS commodity,
                    m.Unit AS unit,
                    m.LimitValue AS limitValue,
                    CONVERT(char(10), m.EffectiveFrom, 23) AS effectiveFrom,
                    CONVERT(char(10), m.EffectiveTo, 23) AS effectiveTo,
                    m.SourceName AS sourceName,
                    m.VersionLabel AS versionLabel,
                    CAST(m.Fictional AS bit) AS fictional
                FROM lab.MrlLimit AS m
                WHERE m.Fictional = 1
                ORDER BY m.MrlId
                FOR JSON PATH, INCLUDE_NULL_VALUES
            ), N'[]')) AS limits
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
    ) AS CatalogJson;
END;
GO

CREATE OR ALTER PROCEDURE rpt.usp_ExportTextCatalog
AS
BEGIN
    SET NOCOUNT ON;
    SELECT (
        SELECT
            N'2026.1' AS catalogVersion,
            JSON_QUERY(ISNULL((
                SELECT
                    t.TextKey AS [key],
                    t.LanguageCode AS language,
                    t.VersionLabel AS versionLabel,
                    t.Scope AS scope,
                    t.Category AS category,
                    CONVERT(char(10), t.EffectiveFrom, 23) AS effectiveFrom,
                    CONVERT(char(10), t.EffectiveTo, 23) AS effectiveTo,
                    t.ApprovalStatus AS approvalStatus,
                    t.Body AS body
                FROM lab.TextCatalog AS t
                ORDER BY t.TextKey, t.VersionLabel
                FOR JSON PATH, INCLUDE_NULL_VALUES
            ), N'[]')) AS entries
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
    ) AS CatalogJson;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'rpt_reader' AND type = N'R')
    CREATE ROLE rpt_reader;
GO

GRANT EXECUTE ON rpt.usp_ListReports TO rpt_reader;
GRANT EXECUTE ON rpt.usp_ExportReportSnapshot TO rpt_reader;
GRANT EXECUTE ON rpt.usp_ExportMrlCatalog TO rpt_reader;
GRANT EXECUTE ON rpt.usp_ExportTextCatalog TO rpt_reader;
GRANT SELECT ON rpt.vw_SampleGrain TO rpt_reader;
GRANT SELECT ON rpt.vw_ResultGrain TO rpt_reader;
GO
