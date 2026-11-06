# Admin screens

The admin pages edit JSON under `App_Data`. They do not compile Razor, do not run SQL typed into a form, and do not fetch a URL.

## What an administrator can change without a deployment

- Template name, paper (`A4` or `Letter`), orientation, margins, samples per group (1–20), and result rows per chunk (8–40)
- Section order, enabled flag, and title
- Which columns of the results table and the fictional-limit table are visible
- Which analyte-group subsections of the results table are enabled
- Add or remove a section whose type is already in `SectionCatalog`
- Add a new approved version of a text key. The previous version stays. The report date selects the version
- Append a fictional limit row for an analyte that already exists on a demo feed. The jurisdiction must match `FICTION-…`, the source name must say fictional, and the unit must be in the small lexicon
- Enable or disable the named feed contract `report-snapshot-v1`

Saving a template writes `App_Data/templates/{id}.json` and a copy under `templates/history/{id}/v{version}.json`. The version number increases. Jobs that already finished keep the pin they stored and do not pick up the new file.

## What needs a developer

A new visual block, for example a chromatogram or a chain-of-custody table, needs all of the following:

1. A `SectionTypeInfo` entry in `SectionCatalog`, including its placement (once per report, or repeated per sample) and its column keys
2. A branch in `Views/Report/Sections/_Dispatch.cshtml`
3. A partial under `Views/Report/Sections/`
4. Any new fields on the snapshot, and matching changes to the JSON feed and `rpt.usp_ExportReportSnapshot`

Until that code exists, the template editor rejects the type name. Pointing the section at a file path or a piece of HTML is not supported. Catalog bodies that contain `<` or `>` are rejected.

A new signatory picture is also a code change. `SignatoryAssets` maps an asset key to a file under `Assets/signatories`. The SVG is scanned for scripts and for URLs other than the SVG namespace. The admin screen only displays the keys that are already registered.

## Feeds

The feed page lists the fields of `report-snapshot-v1` and the procedure name `rpt.usp_ExportReportSnapshot`. A posted SQL string or URL is rejected and is not stored. The JSON adapter reads only files under the seed feed directory for the demo tenant. Identifiers that are not a short token of letters, digits, dots, underscores, and hyphens never become a path.

Switching to SQL is an environment setting, `Data__Provider=Sql`, plus a connection string. It is not a text box in the admin UI. In SQL mode the text and limit catalogs are read from the procedures, and the file editor is turned off so the screen does not pretend to update the database.

## Demo boundary

Every page shows that there is no authentication. Do not expose the site. The synthetic banner on the PDF is not a template option.
