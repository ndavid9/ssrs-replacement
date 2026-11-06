# Pagination

## Samples per group is not “one sheet”

`samplesPerPage` is the maximum number of new samples that start in one group. Examples: 1, 2, or 3. The admin field allows 1 through 20. A group may still occupy several physical pages when a sample has many rows or a long comment.

The group banner states the group number, the sample ids in that group, and the configured maximum. The next group starts with `break-before: page`. The last group does not force a break after itself, which is what usually creates a blank trailing page.

A job can override the template value. The override is stored on that job’s pin and does not rewrite the template.

## Long samples

Result rows are split into chunks (`resultRowsPerChunk`, default 16, 12 on the stress template, allowed range 8–40). Chunk 2 and later are captioned “Sample {id} continued”. Each chunk’s table header repeats the sample id. CSS also sets `thead { display: table-header-group }` and `tr { break-inside: avoid }` so a row is not sliced when a chunk itself crosses a page.

Comments use normal wrapping (`overflow-wrap: anywhere`, `white-space: pre-wrap`) and are allowed to cross pages. They are not scaled down to fit.

There is no `position: fixed` content in the report body. The repeating banner and the “Page X of Y” line are the Playwright header and footer, outside the body margin (top 24 mm, bottom 16 mm, sides 12 mm by default).

## What was checked

Unit tests group the three-sample report at 1, 2, and 3 and the eight-sample stress report at 3 (groups of 3, 3, and 2). HTML tests look for `Sample group n of N`. The stress PDF includes every `DEMO-STR-01` … `DEMO-STR-08`, every `DM-S01` … `DM-S30` (the last of those is in the withheld list because its group is `held`), the continued caption, and the empty-comment line for `DEMO-STR-03`.

`DM-HELD` is withheld on purpose when no enabled subsection names the group `held`. The sample id and analyte code still appear in the withheld note. Disabling a subsection does not delete the snapshot row.

## Footnotes across groups

Method numbers are assigned once, in sample order then result order, for the whole report. They do not restart at a group or a physical page. Result-note markers (`N1`, …) follow the same order. A note is rendered only inside the sample that owns the result.
