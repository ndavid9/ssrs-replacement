# Source inspection

The report layout in this prototype is an original demo. It is not a copy of a laboratory template, and it does not reuse logos, accreditation marks, signatures, or personal data from the sources below. Analytical numbers and limits in the app are fictional.

## Eurofins APAC analytical method summaries

- URL: https://cdnmediaassets.eurofins.com/eurofins-apac/media/5hvkaubh/analytical-method-summaries.pdf
- Retrieved: 5 November 2026, HTTP 200, 1,062,909 bytes, PDF 1.7
- `pdfinfo`: 67 pages, A4, title “Analytical Method Summaries”, creator Microsoft Word for Microsoft 365, dates 11 December 2024
- Text was extracted with Poppler `pdftotext` for the whole file, and a markdown conversion of the same URL was read
- Pages opened as images:
  - PDF page 1: cover, year 2025, title “Analytical Method Summaries”
  - PDF page 2: printed page 1 of 66. Three-column table headed Analyte, Method Summary, Reference Method, with a laboratory group band and a footer (issue date, approval line, organization line, page number)
  - PDF page 62: printed page 61 of 66. A target-analyte table with a limit-of-reporting column and superscript footnotes under the table
  - PDF page 63: printed page 62 of 66. A glossary of symbols and method terms, including LOQ and MDL definitions, with the same footer pattern
- The text extract also shows a final printed page marked intentionally blank. That was not opened as an image
- This document is a method catalog. It is not a sample certificate. The prototype keeps method summaries in their own section, separate from result values. Footer ideas used only as structure: repeated table headings, footnotes, and page X of Y. Wording, colors, and the lab identity are not copied

## Scribd, “AR-SOYA-METALES-organized”

- URL: https://www.scribd.com/document/979197167/AR-SOYA-METALES-organized
- A public document fetch returned a short description and one page-image URL. No paywall or login bypass was attempted
- The image URL for page 1 returned HTTP 200. The same URL pattern for indexes 2 through 6 returned byte-identical JPEGs (MD5 `8db8c4257a7932403853d2439c6d3e46`), so those responses are not additional pages
- Page 1, inspected visually, is a sample metadata sheet: report number, sample code, reception and date fields, portion flags, a client reference, a sample description, and a footer disclaimer that results refer to the checked samples, plus “Page 1 of 4”
- The public description says the document later reports metals below reporting limits. Those result pages were not available here and were not treated as a layout source
- Names, addresses, telephone numbers, and banking lines were visible on page 1 and were not copied into seed data, SQL, or the UI

## Mizoram report PDF

- URL: https://mizothlaithar.mizoram.gov.in/reports/AR-258-2025-20110376-01.pdf
- Requests on 5 November 2026 returned HTTP 403 from this environment, including with a browser user agent. The site root also returned 403
- No pages were read. Nothing in the prototype is based on that file

## What the demo layout actually uses

- A report metadata block and a per-sample identity block, because page 1 of the Scribd preview is that kind of sheet
- Result tables with qualifier, LOD, LOQ, and method marks, because the request asked for them and the method catalog’s glossary treats those as distinct ideas. The column design is original
- A separate method-reference section, because the method PDF is a different document from a results report
- A fictional-limit section that is off until an administrator adds the already-coded handler
- A permanent synthetic banner. It cannot be removed in the admin screens
