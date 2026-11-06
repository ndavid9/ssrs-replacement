# SQL Server contract

The default demo does not open a database. These scripts are the optional adapter. They were reviewed and covered by a text test. They were not executed, because this workspace had no SQL Server and no Docker.

## Objects

`sql/001_schema.sql` creates schemas `lab` and `rpt`.

- `lab.Tenant`, `lab.Principal`
- `lab.Report` and `lab.ReportAccess` (reader or admin, per tenant and report)
- `lab.Sample` with a unique `(TenantId, ReportId, Ordinal)`
- `lab.Analyte`, `lab.MethodReference`
- `lab.Result` with a unique ordinal and a unique analyte per sample, and a check on `ResultKind`
- `lab.MrlLimit` with `Fictional = 1` required
- `lab.TextCatalog` and `lab.TextBinding`
- `lab.Signatory` (reference data; the running app still loads the SVG from disk)

`sql/002_seed.sql` loads the same fictional reports as the JSON feed (`RPT-DEMO-0001`, `RPT-DEMO-STRESS`) plus `RPT-OTHER-0001` on `other-tenant`. `demo-reader` is granted only the demo-tenant reports. `other-reader` is granted the other report. Limits and text versions match the JSON catalogs, including the expired disclaimer and the draft that the resolver must ignore.

`sql/003_views_procs.sql`:

- `rpt.vw_SampleGrain` is one row per sample
- `rpt.vw_ResultGrain` is one row per result. Sample and analyte columns are many-to-one. Do not join this view to `lab.TextBinding`; a sample can have several bindings and the join would multiply results
- `rpt.usp_ListReports @TenantId, @PrincipalId` returns only reports the principal can read
- `rpt.usp_ExportReportSnapshot @TenantId, @ReportId, @PrincipalId` raises an error when access is missing, otherwise returns one JSON document shaped like `ReportSnapshot`. Nested results are built with correlated `FOR JSON` subqueries, not a flat join
- `rpt.usp_ExportMrlCatalog` and `rpt.usp_ExportTextCatalog` return the fictional reference catalogs. They are global in this prototype, not per tenant. Report rows are the data that is access-filtered

The role `rpt_reader` is granted `EXECUTE` on those procedures and `SELECT` on the two views. It is not granted the `lab` tables. `dbo` owns both schemas, so ownership chaining lets the procedure read `lab` while a direct `SELECT` from the role fails for lack of a grant. There is no `DENY`, because a deny would also block chaining.

`sql/004_login.sql` creates login `rpt_app` with the sqlcmd variable `$(AppPassword)`. The password is not in the file. Example:

```bash
sqlcmd -S localhost,1433 -U sa -C -v AppPassword="choose-a-local-password" -i sql/004_login.sql
```

## App settings

```bash
export Data__Provider=Sql
export ConnectionStrings__ReportDb='Server=localhost,1433;Database=LabReportDemo;User Id=rpt_app;Password=choose-a-local-password;Encrypt=True;TrustServerCertificate=True'
```

`SqlReportFeed` sends only the procedure name and `SqlParameter` values. Identifiers are checked against a token pattern before the call. The command text is the constant `rpt.usp_ExportReportSnapshot`.

## Production boundary

A real deployment would authenticate the caller, map that identity to `PrincipalId`, and refuse to trust a tenant id supplied by the browser. The procedure is the backstop, not a substitute for that. It does not accept a report filter expression, a table name, or a tenant list. Snapshots are for one report. There is no “export every tenant” procedure.

`docker-compose.sql.yml` runs `mcr.microsoft.com/mssql/server:2022-latest` and requires `MSSQL_SA_PASSWORD` in the environment. SQL Server’s container license is Microsoft’s, not an open-source license. See `docs/LICENSING.md`.
