# SSRS replacement — Northline lab report prototypes

Open **[README.html](README.html)** for the full write-up (what we tried to achieve, architecture, screenshots, how to run).

Quick links:

| Path | What |
| --- | --- |
| [`prototype/`](prototype/) | ASP.NET Core Razor + Playwright report desk |
| [`admin-mock/`](admin-mock/) | Browser-only layout studio, plus the Nordic examples sandbox |
| [`docs/research.md`](docs/research.md) | Research notes and recommendations |
| [`docs/sample-output/`](docs/sample-output/) | Generated sample PDFs, including `report-with-inserts.pdf` |
| [`media/`](media/) | Walkthrough videos |
| [`screenshots/`](screenshots/) | UI screenshots |

### Run the report desk

```bash
cd prototype
dotnet run --project src/LabReport.Web --urls http://127.0.0.1:5891
```

### Run the layout studio

```bash
python3 -m http.server 8766 --directory admin-mock
```

Then open http://127.0.0.1:8766/

### Nordic examples sandbox

Same server and the same page. Open the **Nordic examples** tab (or http://127.0.0.1:8766/#nordic).

That tab is a separate sandbox. It does not write the layout studio’s `localStorage` key. One fictional snapshot feeds five presets (vertical LOR/MU, grouped tests, sample columns, accreditation footnotes, limit matching). The preview and the exported JSON (`northline-nordic-layout.json`, schema `nordic-sandbox-1.0`) both come from that layout document. Pagination on screen is simulated, not a PDF.

Pattern notes and source links: [`docs/nordic-layout-patterns.md`](docs/nordic-layout-patterns.md).

Checks:

```bash
node admin-mock/selfcheck/run.mjs
```

The report desk can insert external PDFs. A template lists each file, `attachment` or `appendix`, and a slot (`after:` a section, or `end`). Razor leaves a placeholder page there. After Playwright prints, PDFsharp swaps in the PDF and stamps page numbers: report and appendix pages share `Page n of N`, and attachment pages keep their own content with a label such as `Attachment 2 - page 3 of 5`. See `report-with-inserts.pdf` and the page shots under `screenshots/`.

Private repository. All report data is synthetic and is not a laboratory certificate.
