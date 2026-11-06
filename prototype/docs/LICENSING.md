# Licensing

The report desk source in this repository is original demo code written for this prototype.

| Piece | License | Role |
| --- | --- | --- |
| .NET 10 SDK and ASP.NET Core | MIT | Runtime and Razor |
| Microsoft.Data.SqlClient 7.1.1 | MIT | Optional SQL adapter |
| Microsoft.Playwright 1.64.1 | Apache-2.0 | Drives Chromium |
| Playwright driver and Chromium build it downloads | Apache-2.0 for Playwright; Chromium’s BSD-style license for the browser | PDF printing |
| Liberation Sans (regular, bold, italic) | SIL Open Font License 1.1. Reserved font name: Liberation | Embedded in the report HTML. The Debian copyright file is in `src/LabReport.Web/Assets/fonts/SIL-OFL-1.1.txt` |
| xUnit 2.9.3 and the Visual Studio test SDK | Apache-2.0 for xUnit | Tests only |
| Poppler `pdftotext` / `pdftoppm` | GPL | Used on the machine to inspect PDFs. Not referenced by the app |
| SQL Server 2022 container | Microsoft SQL Server license | Optional. Not used by the default demo |

No paid component is required to run the JSON demo.

## Advisory on a transitive package

Restore warns that `System.Security.Cryptography.Pkcs` 9.0.18, pulled in by the SQL client stack, has published advisories GHSA-2fmv-rv2c-59x7 (moderate) and GHSA-846f-f764-76mx (high). The default `Data:Provider` is `Json`, which does not load that client. This prototype does not bump or suppress the package. Treat the SQL adapter as optional until that dependency is updated upstream or pinned past the advisory.

## Fonts

Liberation is embedded as a data URI so Chromium does not request it from the network. Documents produced with the font are allowed by the Open Font License. Do not rename the font files to a name that uses the reserved font name for a derivative.

## Marks that are intentionally absent

The PDFs do not include third-party logos, accreditation symbols, or scanned signatures. The demo mark is an original SVG.
