# Northline report desk

A local ASP.NET Core prototype that turns a versioned report snapshot into HTML with Razor and into a PDF with Microsoft.Playwright Chromium. The data is synthetic. Every sample PDF says it is not a laboratory certificate.

The default path does not need SQL Server. A SQL Server schema, parameterized export procedure, and Docker Compose file are included for the optional adapter. That adapter was not executed in this environment because no SQL Server was available.

## Run the demo

Install the .NET 10 SDK if `dotnet --version` is not 10.x:

```bash
curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
bash /tmp/dotnet-install.sh --channel 10.0 --install-dir "$HOME/.dotnet"
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$DOTNET_ROOT:$DOTNET_ROOT/tools:$PATH"
```

From the repository root:

```bash
dotnet build LabReport.slnx
dotnet test tests/LabReport.Tests/LabReport.Tests.csproj
dotnet run --project src/LabReport.Web --urls http://127.0.0.1:5891
```

Playwright Chromium is required for PDF jobs. After the first build:

```bash
pwsh src/LabReport.Web/bin/Debug/net10.0/playwright.ps1 install chromium
```

If PowerShell is not installed, the driver copied into the build output can install it:

```bash
src/LabReport.Web/bin/Debug/net10.0/.playwright/node/linux-x64/node \
  src/LabReport.Web/bin/Debug/net10.0/.playwright/package/cli.js install chromium
```

On a fresh Linux machine, Chromium also needs the libraries from `playwright install-deps chromium` (that step uses the same CLI and requires privileges).

Open [http://127.0.0.1:5891](http://127.0.0.1:5891). Generate a PDF from the desk, or export the sample files without using the browser:

```bash
dotnet run --project src/LabReport.Web -- --export /tmp/labreport-pdfs
```

There is no login. The site is a local demo and must not be deployed.

## What you can click

- Desk: preview HTML or queue a PDF. Preview does not publish a file.
- Jobs: status, input hash, PDF hash, download, and the pinned HTML.
- Templates: order, enable, columns, subsections, samples per group, and add the fictional-limit section.
- Feeds: the approved `report-snapshot-v1` contract. SQL text and URLs are rejected.
- Texts and limits: versioned plain-text catalog and fictional limit rows.
- Signatories: the bundled demo mark. New image files are a developer change.

## Optional SQL Server

```bash
export MSSQL_SA_PASSWORD='choose-a-local-password'
docker compose -f docker-compose.sql.yml up -d
```

Create the database, then run `sql/001_schema.sql`, `sql/002_seed.sql`, and `sql/003_views_procs.sql`. Create the reader login with `sql/004_login.sql` and `sqlcmd -v AppPassword=...`. Point the app at it with:

```bash
export Data__Provider=Sql
export ConnectionStrings__ReportDb='Server=localhost,1433;Database=LabReportDemo;User Id=rpt_app;Password=...;Encrypt=True;TrustServerCertificate=True'
```

Do not commit that password. See `docs/SQL.md`.

## Documents

- `docs/SOURCE-INSPECTION.md` — which reference pages were actually opened
- `docs/ARCHITECTURE.md` — snapshot, queue, Razor, Playwright
- `docs/ADMIN.md` — what configuration can do, and what needs code
- `docs/HOW-TO-ADD-MRL.md` — adding the fictional-limit section
- `docs/PAGINATION.md` — samples per group versus physical pages
- `docs/SIGNATURES.md` — visual labels versus cryptographic signing
- `docs/SQL.md` — tables, procedure, and the read-only role
- `docs/LICENSING.md` — third-party licenses

## Inserted PDFs

A template can list external PDFs in `inserts` (`file`, `mode` of `attachment` or `appendix`, `slot` of `end` or `after:<section id>`, and a `label`). The demo template is `analytical-inserts`. Its files are `Seed/inserts/chromatogram.pdf` (2 pages), `instrument-log.pdf` (5 pages, page 3 landscape), and `method-appendix.pdf` (3 pages). Razor emits a placeholder page at each slot. After Chromium prints, PDFsharp replaces those pages and stamps `Page n of N` on report and appendix pages. Attachment pages keep their own content and get a label such as `Attachment 2 - page 3 of 5` in a strip added below the original page. The signing hook runs after that stamp and still does not sign. Encrypted or corrupt uploads fail validation. Each file’s SHA-256 is stored beside the pin hash.

The repository file `docs/sample-output/report-with-inserts.pdf` is that demo. Page 1 is `Page 1 of 9`, the two chromatogram pages are attachments, the landscape log is `Attachment 2 - page 3 of 5`, and the supplement continues as `Page 7 of 9` through `Page 9 of 9`.

## Tests

`dotnet test tests/LabReport.Tests/LabReport.Tests.csproj` covers grouping at 1, 2, and 3 samples, placeholder failures, sample-scoped text, method deduplication, MRL unit rules, template persistence, HTML encoding, insert numbering, slot replacement, and real Chromium PDFs. The last run in this workspace was 31 passed, 0 failed. SQL execution was not part of that run.
