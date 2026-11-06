# Research notes — SSRS replacement for laboratory analytical PDFs

These notes summarize what was researched and recommended while building the Northline prototypes in this repository. They are grounded in the source inspection document (`docs/prototype/SOURCE-INSPECTION.md`), the architecture and licensing docs under `docs/prototype/`, and the delivered prototypes. Facts that were not verified are called out as such.

## Problem

Laboratories publish legally significant analytical reports as PDFs. Many shops generate those PDFs with SQL Server Reporting Services (SSRS), often via URL-access export. SSRS works, but complex multi-sample layouts, new sections (for example MRL comparisons), scoped legal text, pagination rules, and signature handling become expensive to maintain in RDL. The ask was for **inexpensive, easy** alternatives — not a paid reporting suite.

## What public Eurofins-style material actually showed

Only publicly fetchable pages were opened. Details and URLs are in `docs/prototype/SOURCE-INSPECTION.md`.

| Source | Outcome |
| --- | --- |
| Eurofins APAC *Analytical Method Summaries* PDF | Retrieved (67 pages, A4). Method catalog with analyte / method / reference tables, LOR footnotes, glossary (LOQ, MDL), repeated footers. **Not** a sample certificate. |
| Scribd AR-SOYA-METALES preview | Only page 1 was usable as an image: sample metadata sheet (report number, sample code, dates, disclaimer, “Page 1 of 4”). Later result pages were not available here. Personal data on that page was **not** copied into seed data. |
| Mizoram AR-258-2025 PDF | HTTP 403 from this environment. **Nothing** in the prototype is based on that file. |

**Observed structure (not assumptions):** metadata block, per-sample identity, result tables with qualifier / LOD / LOQ / method marks, separate method-reference section, footnotes, page X of Y. Accreditation marks and real signatures were intentionally **absent** from the demo.

Whether Eurofins (or any specific lab) uses SSRS internally was **not** proven from these public files. The user’s premise that SSRS is common for this class of report was taken as the product context, not as a verified claim about any named lab.

## Inexpensive options considered

Constraint from the user: do not recommend expensive tools; prefer an easy path.

| Option | Role | Cost / license note |
| --- | --- | --- |
| **Keep SSRS for production certificates (short term)** | Continue publishing known-good legal PDFs | Already owned / operated |
| **Razor HTML + Microsoft.Playwright Chromium `page.pdf()`** | HTML-first layout, print to PDF in a background worker | .NET MIT; Playwright Apache-2.0; Chromium BSD-style. **Chosen for the working prototype** |
| **WeasyPrint** | HTML/CSS → PDF without a browser | Free / open source; good for simpler print CSS; less control than Chromium for complex interactive HTML |
| **QuestPDF** | Code-first PDF in .NET | Community License is **conditional** (revenue / org-size limits). Not used in the prototype; check current QuestPDF terms before adopting |
| Paid report designers / cloud report SaaS | Full product | Out of scope per “don’t recommend expensive tools” |

**Recommendation (hybrid):** keep working SSRS for production certificates; **pilot** Razor + Playwright for new or dynamic layouts (MRL, multi-sample text, admin-curated sections). Migrate certificate types only after side-by-side PDF comparison and legal sign-off.

No paid component is required to run the JSON-backed demo (`docs/prototype/LICENSING.md`).

## How other companies handle complex PDFs (patterns, not named vendors)

Industry practice for regulated or high-stakes PDFs tends to share these patterns (general industry knowledge applied to this design; not a claim that any specific company was reverse-engineered):

1. **Immutable snapshots** — freeze the data that went into a published PDF; do not re-query live LIMS for a reprint.
2. **Content hashes and supersedes** — SHA-256 of the pinned input (and of the PDF bytes); a new version links to what it replaces.
3. **Queued background render** — UI enqueues; workers print; concurrency and timeouts are capped.
4. **Declarative layout config** — versioned JSON/templates with a **whitelist** of section handlers; admins reorder/enable, developers add new visual types.
5. **Named data contracts** — SQL views / stored procedures as feeds (`rpt.vw_*`, `rpt.usp_ExportReportSnapshot`); no arbitrary SQL from admin UI.
6. **Scoped text catalogs** — comments, conclusions, disclaimers with typed placeholders; HTML-encoded; fail validation on unresolved tokens.
7. **Signatory blocks, not string replacement** — render a registered asset / block; cryptographic signing is a **separate** step after render (detached signature / timestamp), not find-and-replace of a name string inside the PDF.
8. **Pagination rules as config** — e.g. `maxSamplesPerGroup` with continuation pages; physical page breaks still owned by the print engine.

The Northline prototype implements these ideas in miniature; see `docs/prototype/ARCHITECTURE.md`.

## Architecture implemented in the prototype

```
Authorize demo principal
    → load named feed contract (JSON seed or optional SQL export proc)
    → validate snapshot + resolve approved text + evaluate fictional limits
    → pin snapshot + template + text versions + signatory (SHA-256 input hash)
    → BackgroundService / in-memory channel (concurrency capped)
    → Razor section partials (whitelist dispatch)
    → Playwright Chromium PDF (network aborted except data/about/blob)
    → store PDF + PDF hash; signer stub records that crypto is a later hook
```

**Pagination:** `samples per group` (config, 1–20) with continuation groups. Physical pages are Chromium’s; see `docs/prototype/PAGINATION.md`.

**MRL / fictional limits:** match by analyte, matrix, jurisdiction, commodity, unit, effective dates. Units are **not** converted; mismatch → “not evaluated”. Section is off until the admin adds the already-coded handler (`docs/prototype/HOW-TO-ADD-MRL.md`).

**SQL:** schema, seed, views/procs, and login scripts live under `prototype/sql/`. They were **never executed** against a SQL Server in the environment that built this demo. Default feed is JSON.

## Most complex use case you will struggle with

The hard case is **not** “draw a table.” It is the combination of:

1. **Many samples** on one certificate with different tests / parameters / results, plus **sample-scoped** comments and conclusions that must land with the right sample after grouping.
2. **Optional sections** (MRL, accreditation) that appear only for some report types / matrices / jurisdictions, while required sections stay required.
3. **Legal text versioning** — the correct approved disclaimer for the report date, with placeholders that fail closed if unresolved.
4. **Limit evaluation that refuses to invent a pass/fail** when units differ or LOQ is above the limit.
5. **Reprint fidelity** — changing a template later must not silently change an already-published PDF (pins + hashes).
6. **Signatures** — visual mark vs cryptographic proof; treating them as string substitution is the trap.

That is why the layout studio models domains (Report, Sample, Tests, Parameters, Results, Comments, Conclusions, MRL, Accreditation) with multiplicities and validation, instead of a free-form HTML editor.

## Domain multiplicities (layout studio)

High-level ER used in the admin mock:

- Report **1** → Sample **1..\*** → Test **1..\*** → Parameter **1..\*** → Result **0..1**
- Comments / Conclusions / Accreditation / MRL attach with **0..\*** (and scope rules: report vs sample vs result)

Feeds stay on named contracts such as `report-snapshot-v1` / `mrl-fictional-v1`. The studio will not store a URL or a SQL string. Sample export: `docs/sample-layout.json`.

## Honest limitations of what was delivered

- All analytical data is **synthetic**; every PDF banner says it is not a laboratory certificate.
- Optional SQL scripts were **not** run against SQL Server.
- Layout studio is **localStorage only** — no backend.
- Demo walkthrough video does **not** open the generated PDFs on screen (PDFs are in `docs/sample-output/`).
- No real IdP, no cryptographic signing, no PDF/A, no durable external queue.
- New visual section types still need a developer (catalog + Razor partial); admins cannot invent arbitrary HTML.

## Pointers

- Prototype runbook: `prototype/README.md`
- Architecture: `docs/prototype/ARCHITECTURE.md`
- Source inspection: `docs/prototype/SOURCE-INSPECTION.md`
- Layout studio: `admin-mock/README.md`
