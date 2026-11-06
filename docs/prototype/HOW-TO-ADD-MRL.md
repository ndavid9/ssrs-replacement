# Adding the fictional-limit section

The measured results and the limit rows can both be present while the PDF still omits the comparison. The section is a handler that already exists. Including it is a template edit.

## Before

`Seed/templates/analytical-default.json` version 1 does not contain `mrl-comparison`. A preview or PDF of `RPT-DEMO-0001` with that template has result tables and no “Fictional limit comparison” heading.

The limit rows are already in `Seed/catalogs/mrl.json` (and in `lab.MrlLimit` if you load the SQL seed). They are labeled fictional, use jurisdiction `FICTION-ZONE-7`, and are not regulatory values.

## Add it in the admin UI

1. Open Templates and edit “Analytical demonstration”.
2. In “Add a supported section”, choose “Fictional limit comparison” and click Add section.
3. Save layout if you also changed columns or the samples-per-group number.
4. Generate `RPT-DEMO-0001` again with the updated template.

The template version becomes 2. `App_Data/templates/history/analytical-default/v2.json` keeps that copy. The previous PDF, if one was already generated, stays on its old pin and does not grow a limit table.

The same operation is what `TemplateEditor.AddSection` does. The export command uses it to write a second template id, `analytical-with-mrl`, without modifying the seeded default. Tests render both and check that the heading and the sentence “Numeric value is within the fictional limit.” appear only after the section is saved.

## What the section shows

For each result on the sample, one row:

| Situation | Label |
| --- | --- |
| Numeric value, same unit, at or under the limit | Numeric value is within the fictional limit. |
| Numeric value, same unit, over the limit | Numeric value exceeds the fictional limit. |
| Below LOQ and the LOQ is at or under the limit | Below LOQ. The LOQ does not exceed the fictional limit. This is not a numeric pass. |
| Below LOQ and the LOQ is above the limit | Inconclusive. |
| ND | Not a numeric pass or fail. |
| Not tested, missing value, missing unit | No comparison. |
| Limit unit differs, including mg/kg versus g/kg | Units differ. No pass or fail. |
| No row matches analyte, matrix, commodity, jurisdiction, and dates | No applicable fictional limit. |
| Two rows tie on the effective date | Ambiguous. No comparison. |

`DM-CD` on `DEMO-SMP-01` is the unit-mismatch example (`mg/kg` result, `g/kg` limit). `DM-ZN` has only an expired limit and a limit in `FICTION-ZONE-9`, so the report jurisdiction finds nothing. `DM-PB` on `DEMO-SMP-02` is a numeric exceed of the fictional 1.11 mg/kg value.

## A new limit row versus a new section

Appending a row on the Limits page adds data. It does not add a visual section. The analyte code has to already exist on a feed, and the jurisdiction has to start with `FICTION-`. That constraint is deliberate so the form cannot be labeled as an official limit.

If the comparison needed a different shape, such as a chart or a multi-jurisdiction matrix, that would be a new section type and would need the code changes in `docs/ADMIN.md`.
