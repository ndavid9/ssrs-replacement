# Architecture

## Pipeline

1. The desk sends a report id, a template id, and an optional samples-per-group override.
2. `DemoAuthorizer` allows only the configured demo tenant and principal. The form cannot name another tenant.
3. The feed loads a canonical snapshot. The JSON feed reads `Seed/feeds/report-snapshot-v1/{tenant}/{reportId}.json`. The SQL feed calls `rpt.usp_ExportReportSnapshot` with `@TenantId`, `@ReportId`, and `@PrincipalId`.
4. The planner validates the snapshot, resolves approved text for the report date, evaluates fictional limits, and numbers methods.
5. If validation fails, the job is stored as `FailedValidation` and no PDF is written.
6. If it passes, the exact snapshot, template, selected text versions, limit rows, signatory block, and the SHA-256 of each insert file are written to `App_Data/pins/{jobId}.json`. A SHA-256 of that pin is the input hash.
7. A `BackgroundService` reads an in-memory channel. Concurrency is capped (default 2). PDF printing itself is serialized on one browser. Attempts default to 3. A render timeout defaults to 90 seconds. Restarting the process re-queues jobs that were still `Queued` or `Rendering`.
8. Razor renders section partials. Playwright loads that HTML with network and file requests aborted except `data:`, `about:`, and `blob:`. Liberation Sans is embedded so the PDF does not fetch a font. The footer reserves an empty box for the page number. It does not print one.
9. If the template lists inserts, each slot page is replaced with the uploaded PDF (PDFsharp). Page numbers are stamped after that merge. `NoCryptographicSigner` runs last on those bytes and does not sign. The stored PDF hash is the signed-hook result. Each inserted file’s SHA-256 is stored on the job next to the pin hash.
10. Download uses the job GUID only. Paths are combined under `App_Data/artifacts` and rejected if they escape that directory.

The same input hash in `Queued`, `Rendering`, `Succeeded`, or `FailedValidation` returns the existing job. A failed render can be queued again as a new job. Editing a template or catalog changes the hash, so a new job is created and the old PDF stays put.

## Snapshot

`ReportSnapshot` schema `1.0` is the contract shared by the JSON files and the SQL procedure. Samples are ordered by ordinal, then id. Results live on the sample and are ordered the same way. A result kind is `numeric`, `belowLoq`, `notDetected`, `notTested`, or `missing`. A numeric kind requires a value. Below LOQ requires an LOQ. The other kinds must not also carry a numeric value.

## Sections

Handlers are a fixed whitelist in `SectionCatalog`. The Razor view switches on that same set of type strings and throws for anything else. Configuration can add, remove, reorder, enable, retitle, and hide columns or subsections for those types. It cannot name a view, a URL, or a SQL string.

Sample sections (`sample-summary`, `analytical-results`, `mrl-comparison`, `comments`, `conclusions`) repeat inside each sample group. Report sections at or above the first sample section print once before the groups. Later report sections print once after the groups. A report section cannot be inserted between two samples.

An identity line with the sample id is always printed, even when the summary section is disabled, so a sample cannot vanish because of layout. Results that fall outside an enabled subsection are listed as withheld, with the analyte code, rather than dropped from the snapshot.

## Text

Catalog entries have a key, language, version, scope, category, effective dates, and an approval status. The resolver picks the latest approved version effective on the report date. Drafts and expired rows are ignored. Placeholders are `{{token}}` and are allowed only for the entry’s scope. Replacement happens inside that string. The result is normal text, and Razor encodes it. There is no `Html.Raw` in the report views. A missing or unknown placeholder, or a `<` or `>` in a catalog body, fails validation.

Comments and sample conclusions are bound to a sample id. Result notes are bound to a result id and numbered `N1`, `N2`, … in sample and result order. Method notes are bound to a method code. The report disclaimer and the report conclusion print once.

## Limits

Limits must be marked fictional, use a `FICTION-` jurisdiction, name a source that says “Fictional”, and use a known unit (`mg/kg`, `ug/kg`, `g/kg`, `mg/l`, `%`). Matching requires analyte, jurisdiction, matrix, commodity, and effective dates. Units are not converted. `ug/kg` and `mg/kg` are different. A numeric within/exceeds label is used only when the value and the limit share a unit. ND, not tested, missing, unit mismatch, an LOQ above the limit, and more than one matching limit do not receive a pass or fail.

## Tenancy

The demo authorizer is not authentication. Production needs a real caller identity, a check that the caller may read that report, and no cross-tenant query. The SQL procedure is the second gate: it returns a row only when `lab.ReportAccess` matches the tenant, report, and principal. The JSON feed only has `demo-tenant` files. See `docs/SQL.md`.

## Not in this prototype

- A real identity provider, audit of human approvers, or customer tenancy
- A durable external queue. The channel is in-process. A process crash after `Succeeded` is safe; a crash during `Rendering` renders again from the same pin
- Cryptographic signing, PDF/A, or a timestamp service
- Arbitrary new section renderers without a code change
- Proof that the optional SQL scripts run. They were not executed here
