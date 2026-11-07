# Nordic layout patterns

The **Nordic examples** tab in `admin-mock/` is a sandbox for shapes seen in public Nordic analytical PDFs. It sits beside the layout studio and does not change that editor.

All sample values, names, and the review block are fictional. This repository does not contain those PDFs, their logos, their customer data, or their signatures. The links below are a reading list.

## What the sandbox is demonstrating

One snapshot, `nordic-snapshot-v1` on the feed `report-snapshot-v1`, feeds every preset. A preset is a versioned layout document (`nordic-sandbox-1.0`), not a separate HTML report. The preview and `northline-nordic-layout.json` are both produced from that document.

| Domain | Multiplicity | Feed | How the sandbox uses it |
| --- | --- | --- | --- |
| Report | 1 | `report-snapshot-v1` | First-page block, plus a running mock header |
| Sample | 1..* | `report-snapshot-v1` | Stacked blocks, or columns when the layout is a matrix |
| Test | 1..* | `report-snapshot-v1` | Optional group header over its parameters |
| Parameter | 1..* | `report-snapshot-v1` | Row identity shared by every selected sample |
| Result | 0..1 | `report-snapshot-v1` | One slot per sample and parameter. Empty slot is “not reported” |
| Comments | 0..* | `text-catalog-v1` | Report, sample, test, or result. Printed on the page that owns that scope |
| Conclusions | 0..* | `text-catalog-v1` | Report conclusions are end-matter on the last simulated page |
| MRL | 0..* | `mrl-fictional-v1` | Scored only when unit, matrix, and jurisdiction all match |
| Accreditation | 0..* | `accreditation-catalog-v1` | Footnote on the simulated pages that show that method |

Markers stay different on purpose:

- **not detected** — below LOR or LOQ
- **not reported** — the result slot, the uncertainty, or a reporting limit is empty
- **Not accredited** — the method is outside the fictional scope; the result is still shown
- **not evaluated** — a limit exists but unit, matrix, or jurisdiction does not match, or the result cannot be scored. This is never a pass or fail
- **hidden by layout** — the domain is turned off

Turning a match rule off is a blocking error. The preview then says the evaluation is blocked.

## Presets

| Preset | Layout idea |
| --- | --- |
| Harbour screen | One water sample, vertical rows, MU, unit, LOR, method |
| Basin groups | Grouped tests, uncertainty, continuation, sample and test notes |
| Plot comparison | Eight soil samples, four columns per page, shared parameter rows |
| Material footnotes | LOQ, MU, method, accreditation note only where that method is visible |
| Flush check | Reference text, LOQ, limit, and match-or-not-evaluated |

Samples per page can be raised past four. That is a stress case: headers still repeat, and the sheet scrolls sideways. It is a warning, not a second renderer.

## Pagination gap

Page breaks in this tab are **simulated**. The layout slices sample groups horizontally and result rows vertically, repeats the sample and column headers, keeps notes with the scope that owns them, and prints the report sign-off only on the last simulated page.

This tab does not call a print engine and does not produce a PDF. `pagination.pdf` in the export is `false`. Physical pages, margin headers, and page X of Y remain the report desk’s job (Playwright), described in `docs/prototype/PAGINATION.md`.

## Reference list

Public files used as layout-pattern reading only:

- Vertical water table (parameter, result, measurement uncertainty, unit, LOR, method). ALS, Stockholm meetings archive. <https://meetingspublic.stockholm.se/welcome-sv/namnder-styrelser/skonhetsradets-delegerade-i-plan-och-bygglovsarenden-naturvardsarenden-samt-kulturvardsarenden/mote-2021-03-22/protocol/grundvattenprovtagningpdf?downloadMode=open>
- Grouped groundwater tests, uncertainty, methods, continuation, and comments. SGS report linked from Växjö. <https://www.vaxjo.se/download/18.28f2020119b3545d11a82440/1767539951008/Rapport%20grundvattenprovtagning,%20WSP,%202025-09-05.pdf>
- Several soil samples side by side on shared parameter rows. SYNLAB report linked from Porvoo. <https://www.porvoo.fi/app/uploads/2021/11/Selvitys-4-100-FCG-PiMa-tutkimusraportti-poistettu-oljysailio-26.2.2021.pdf>
- LOQ, measurement uncertainty, method, and accreditation footnotes on a solids sample. Eurofins Norway file published via NVE. <https://webfileservice.nve.no/API/PublishedFiles/Download/e6bb5dd1-3a23-4987-9c13-90962ebf4449/202419314/3447562>
- Result, guidance limit, quantification limit, reference, and method. AGROLAB report linked from nov.dk (issuer in Germany). <https://www.nov.dk/Portals/299/BEFUND30_W-2479833_Vers_1%20AAGV%2C%20Flushprove%2C%20ledningsnetprove%2022_08_2025.pdf>

## Run

```bash
python3 -m http.server 8766 --directory admin-mock
```

Open <http://127.0.0.1:8766/#nordic>.

```bash
node admin-mock/selfcheck/run.mjs
```
