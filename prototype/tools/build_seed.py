#!/usr/bin/env python3
"""Write the checked-in synthetic feeds, catalogs, templates, and SQL seed."""

from __future__ import annotations

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SEED = ROOT / "src" / "LabReport.Web" / "Seed"
SQL = ROOT / "sql"

UPDATED = "2026-01-15T00:00:00Z"


def result(result_id, sample_id, code, name, group, ordinal, kind, method, unit=None, value=None, qualifier=None, lod=None, loq=None):
    return {
        "resultId": result_id,
        "sampleId": sample_id,
        "analyteCode": code,
        "analyteName": name,
        "analyteGroup": group,
        "ordinal": ordinal,
        "kind": kind,
        "numericValue": value,
        "qualifier": qualifier,
        "lod": lod,
        "loq": loq,
        "unit": unit,
        "methodCode": method,
    }


def sample(sample_id, ordinal, description, *, comment_ready=True):
    return {
        "sampleId": sample_id,
        "sampleCode": sample_id,
        "ordinal": ordinal,
        "description": description,
        "matrix": "oil",
        "commodity": "refined-demo-oil",
        "samplingDate": "2026-04-01",
        "receivedDate": "2026-04-02",
        "receptionTemperature": "Ambient demo",
        "babyFood": False,
        "organic": False,
        "results": [],
    }


NAMES = {
    "DM-PB": ("Fictional lead analogue", "metals", "DEMO-MET-01"),
    "DM-AS": ("Fictional arsenic analogue", "metals", "DEMO-MET-01"),
    "DM-CU": ("Fictional copper analogue", "metals", "DEMO-MET-01"),
    "DM-FE": ("Fictional iron analogue", "screen", "DEMO-MET-02"),
    "DM-CD": ("Fictional cadmium analogue", "metals", "DEMO-MET-01"),
    "DM-ZN": ("Fictional zinc analogue", "screen", "DEMO-MET-02"),
    "DM-NI": ("Fictional nickel analogue", "metals", "DEMO-MET-01"),
    "DM-HG": ("Fictional mercury analogue", "metals", "DEMO-MET-03"),
    "DM-HELD": ("Fictional held analyte", "held", "DEMO-MET-02"),
    "DM-TAG": ("Fictional tag <demo>", "screen", "DEMO-MET-02"),
}


def R(sample_id, ordinal, code, kind, **kwargs):
    name, group, method = NAMES[code]
    short = code.split("-", 1)[1]
    return result(
        f"RES-{sample_id}-{short}",
        sample_id,
        code,
        name,
        group,
        ordinal,
        kind,
        method,
        **kwargs,
    )


METHODS = [
    {
        "methodCode": "DEMO-MET-01",
        "methodName": "Fictional digest screen A",
        "summary": "A fictional bench procedure used only to show a method note beside a result. It is not a published method.",
        "referenceCitation": "Fictional Demo Method Index (2026), entry D-17.",
        "versionLabel": "demo-1",
    },
    {
        "methodCode": "DEMO-MET-02",
        "methodName": "Fictional screen B",
        "summary": "A second fictional procedure so method references can be deduplicated across samples.",
        "referenceCitation": "Fictional Demo Method Index (2026), entry D-18.",
        "versionLabel": "demo-1",
    },
    {
        "methodCode": "DEMO-MET-03",
        "methodName": "Fictional trace screen C",
        "summary": "A third fictional procedure with no extra catalog note, so the section still lists its citation.",
        "referenceCitation": "Fictional Demo Method Index (2026), entry D-19.",
        "versionLabel": "demo-1",
    },
]


def report(report_id, number, title, samples, bindings, revision):
    used = {res["methodCode"] for sample_row in samples for res in sample_row["results"] if res["methodCode"]}
    methods = [method for method in METHODS if method["methodCode"] in used]
    return {
        "schemaVersion": "1.0",
        "tenantId": "demo-tenant",
        "reportId": report_id,
        "reportNumber": number,
        "title": title,
        "reportDate": "2026-04-15",
        "matrix": "oil",
        "commodity": "refined-demo-oil",
        "clientName": "Northline Pantry Cooperative",
        "clientReference": "DEMO-PO-1001",
        "origin": "Fictional Harbor District",
        "orderedBy": "Demo Purchasing Desk",
        "submittedBy": "Demo Intake Desk",
        "sourceFeed": "report-snapshot-v1",
        "sourceRevision": revision,
        "samples": samples,
        "methods": methods,
        "textBindings": bindings,
    }


def normal_report():
    s1 = sample("DEMO-SMP-01", 1, "Fictional refined oil portion A.")
    s1["results"] = [
        R("DEMO-SMP-01", 1, "DM-PB", "numeric", unit="mg/kg", value=0.4, lod=0.01, loq=0.05),
        R("DEMO-SMP-01", 2, "DM-AS", "belowLoq", unit="mg/kg", qualifier="<", lod=0.01, loq=0.02),
        R("DEMO-SMP-01", 3, "DM-CU", "notDetected", unit="mg/kg", qualifier="ND", lod=0.01, loq=0.05),
        R("DEMO-SMP-01", 4, "DM-FE", "notTested"),
        R("DEMO-SMP-01", 5, "DM-CD", "numeric", unit="mg/kg", value=1.25, lod=0.01, loq=0.05),
        R("DEMO-SMP-01", 6, "DM-ZN", "numeric", unit="mg/kg", value=0.1, lod=0.02, loq=0.05),
        R("DEMO-SMP-01", 7, "DM-NI", "missing"),
        R("DEMO-SMP-01", 8, "DM-HG", "numeric", unit="mg/kg", value=0.02, lod=0.005, loq=0.01),
        R("DEMO-SMP-01", 9, "DM-HELD", "numeric", unit="mg/kg", value=0.3, lod=0.01, loq=0.05),
    ]
    s2 = sample("DEMO-SMP-02", 2, "Fictional refined oil portion B.")
    s2["results"] = [
        R("DEMO-SMP-02", 1, "DM-PB", "numeric", unit="mg/kg", value=4.5, lod=0.01, loq=0.05),
        R("DEMO-SMP-02", 2, "DM-AS", "belowLoq", unit="mg/kg", qualifier="<", lod=0.01, loq=5),
        R("DEMO-SMP-02", 3, "DM-CU", "numeric", unit="mg/kg", value=0.2, lod=0.01, loq=0.05),
        R("DEMO-SMP-02", 4, "DM-TAG", "numeric", unit="mg/kg", value=0.15, lod=0.01, loq=0.05),
    ]
    s3 = sample(
        "DEMO-SMP-03",
        3,
        "Fictional portion C. The characters {{reportNumber}} are ordinary sample text.",
    )
    s3["results"] = [
        R("DEMO-SMP-03", 1, "DM-FE", "numeric", unit="mg/kg", value=0.8, lod=0.05, loq=0.1),
        R("DEMO-SMP-03", 2, "DM-HG", "numeric", unit="mg/kg", value=0.9, lod=0.005, loq=0.01),
    ]
    bindings = [
        {"bindingId": "b-disclaimer", "textKey": "disclaimer.scope", "scope": "report", "sampleId": None, "resultId": None, "methodCode": None},
        {"bindingId": "b-conclusion-report", "textKey": "conclusion.report", "scope": "report", "sampleId": None, "resultId": None, "methodCode": None},
        {"bindingId": "b-comment-01", "textKey": "comment.oil-a", "scope": "sample", "sampleId": "DEMO-SMP-01", "resultId": None, "methodCode": None},
        {"bindingId": "b-conclusion-01", "textKey": "conclusion.oil-a", "scope": "sample", "sampleId": "DEMO-SMP-01", "resultId": None, "methodCode": None},
        {"bindingId": "b-comment-02", "textKey": "comment.oil-b", "scope": "sample", "sampleId": "DEMO-SMP-02", "resultId": None, "methodCode": None},
        {"bindingId": "b-conclusion-02", "textKey": "conclusion.oil-b", "scope": "sample", "sampleId": "DEMO-SMP-02", "resultId": None, "methodCode": None},
        {"bindingId": "b-note-pb", "textKey": "result.note.pb", "scope": "result", "sampleId": "DEMO-SMP-01", "resultId": "RES-DEMO-SMP-01-PB", "methodCode": None},
        {"bindingId": "b-method-01", "textKey": "method.note.01", "scope": "method", "sampleId": None, "resultId": None, "methodCode": "DEMO-MET-01"},
    ]
    return report(
        "RPT-DEMO-0001",
        "DEMO-AR-1001",
        "Synthetic metals screen",
        [s1, s2, s3],
        bindings,
        "json-seed-1",
    )


def stress_report():
    long_comment_body_marker = "STRESS-LONG"
    samples = []
    for index in range(1, 9):
        sid = f"DEMO-STR-{index:02d}"
        description = f"Fictional stress portion {index:02d}."
        if index == 2:
            description += " " + ("The bench note is intentionally long so the comment can cross a physical page. " * 40)
        item = sample(sid, index, description.strip())
        if index == 1:
            rows = []
            for n in range(1, 31):
                code = f"DM-S{n:02d}"
                kind_cycle = ["numeric", "belowLoq", "notDetected", "notTested", "missing"][n % 5]
                group = "held" if n == 30 else ("metals" if n <= 15 else "screen")
                method = "DEMO-MET-01" if n % 2 else "DEMO-MET-02"
                unit = "mg/kg" if kind_cycle in ("numeric", "belowLoq", "notDetected") else None
                value = round(0.05 * n, 2) if kind_cycle == "numeric" else None
                loq = 0.02 if kind_cycle in ("numeric", "belowLoq", "notDetected") else None
                lod = 0.01 if loq else None
                qualifier = "<" if kind_cycle == "belowLoq" else ("ND" if kind_cycle == "notDetected" else None)
                rows.append(result(
                    f"RES-ST-{n:02d}",
                    sid,
                    code,
                    f"Fictional stress analyte {n:02d}",
                    group,
                    n,
                    kind_cycle,
                    method,
                    unit=unit,
                    value=value,
                    qualifier=qualifier,
                    lod=lod,
                    loq=loq,
                ))
            item["results"] = rows
        elif index == 4:
            item["results"] = [
                R(sid, 1, "DM-PB", "numeric", unit="mg/kg", value=0.2, lod=0.01, loq=0.05),
                R(sid, 2, "DM-HELD", "numeric", unit="mg/kg", value=0.2, lod=0.01, loq=0.05),
            ]
        else:
            item["results"] = [
                R(sid, 1, "DM-PB", "numeric", unit="mg/kg", value=0.2 + index / 100, lod=0.01, loq=0.05),
                R(sid, 2, "DM-CU", "notDetected", unit="mg/kg", qualifier="ND", lod=0.01, loq=0.05),
            ]
        samples.append(item)
    bindings = [
        {"bindingId": "b-stress-disclaimer", "textKey": "disclaimer.scope", "scope": "report", "sampleId": None, "resultId": None, "methodCode": None},
        {"bindingId": "b-stress-conclusion-report", "textKey": "conclusion.report", "scope": "report", "sampleId": None, "resultId": None, "methodCode": None},
    ]
    for index in range(1, 9):
        if index == 3:
            continue
        sid = f"DEMO-STR-{index:02d}"
        key = "comment.stress.long" if index == 2 else "comment.stress"
        bindings.append({
            "bindingId": f"b-stress-comment-{index:02d}",
            "textKey": key,
            "scope": "sample",
            "sampleId": sid,
            "resultId": None,
            "methodCode": None,
        })
    bindings.append({
        "bindingId": "b-stress-method",
        "textKey": "method.note.01",
        "scope": "method",
        "sampleId": None,
        "resultId": None,
        "methodCode": "DEMO-MET-01",
    })
    _ = long_comment_body_marker
    return report(
        "RPT-DEMO-STRESS",
        "DEMO-AR-STRESS",
        "Synthetic stress screen",
        samples,
        bindings,
        "json-seed-stress-1",
    )


def limits():
    def row(mrl_id, code, unit, value, start, end=None, jurisdiction="FICTION-ZONE-7", commodity="refined-demo-oil"):
        return {
            "mrlId": mrl_id,
            "analyteCode": code,
            "jurisdiction": jurisdiction,
            "matrix": "oil",
            "commodity": commodity,
            "unit": unit,
            "limitValue": value,
            "effectiveFrom": start,
            "effectiveTo": end,
            "sourceName": "Fictional Demo Residue Catalog",
            "versionLabel": "fic-2026.1",
            "fictional": True,
        }

    rows = [
        row("mrl-pb", "DM-PB", "mg/kg", 1.11, "2026-01-01"),
        row("mrl-pb-other-commodity", "DM-PB", "mg/kg", 0.01, "2026-01-01", commodity="crude-demo-oil"),
        row("mrl-as", "DM-AS", "mg/kg", 0.8, "2026-01-01"),
        row("mrl-cu", "DM-CU", "mg/kg", 3.33, "2026-01-01"),
        row("mrl-cu-future", "DM-CU", "mg/kg", 0.01, "2027-01-01"),
        row("mrl-cd-mismatch", "DM-CD", "g/kg", 2, "2026-01-01"),
        row("mrl-zn-expired", "DM-ZN", "mg/kg", 9.99, "2025-01-01", "2025-12-31"),
        row("mrl-zn-other-zone", "DM-ZN", "mg/kg", 0.01, "2026-01-01", jurisdiction="FICTION-ZONE-9"),
        row("mrl-hg", "DM-HG", "mg/kg", 0.55, "2026-01-01"),
        row("mrl-fe", "DM-FE", "mg/kg", 8, "2026-01-01"),
        row("mrl-s01", "DM-S01", "mg/kg", 0.2, "2026-01-01"),
    ]
    return {"catalogVersion": "fic-2026.1", "limits": rows}


def texts():
    long_body = (
        "Stress comment for sample {{sampleId}}. "
        + ("This sentence is padding so the comment flows across the page without being scaled or clipped. " * 28)
    )
    entries = [
        {
            "key": "disclaimer.scope",
            "language": "en",
            "versionLabel": "0.9.0",
            "scope": "report",
            "category": "disclaimer",
            "effectiveFrom": "2025-01-01",
            "effectiveTo": "2025-12-31",
            "approvalStatus": "approved",
            "body": "OLD TEXT SHOULD NOT APPEAR token OLD-DISCLAIMER-9 for {{reportNumber}}.",
        },
        {
            "key": "disclaimer.scope",
            "language": "en",
            "versionLabel": "1.0.0",
            "scope": "report",
            "category": "disclaimer",
            "effectiveFrom": "2026-01-01",
            "effectiveTo": None,
            "approvalStatus": "approved",
            "body": "Synthetic demonstration only for report {{reportNumber}}. Sample count on this document: {{sampleCount}}. This is not a laboratory certificate and is not valid for trade or legal use.",
        },
        {
            "key": "disclaimer.scope",
            "language": "en",
            "versionLabel": "9.9.9",
            "scope": "report",
            "category": "disclaimer",
            "effectiveFrom": "2026-01-01",
            "effectiveTo": None,
            "approvalStatus": "draft",
            "body": "DRAFT TEXT SHOULD NOT APPEAR token DRAFT-DISCLAIMER for {{reportNumber}}.",
        },
        {
            "key": "conclusion.report",
            "language": "en",
            "versionLabel": "1.0.0",
            "scope": "report",
            "category": "conclusion",
            "effectiveFrom": "2026-01-01",
            "effectiveTo": None,
            "approvalStatus": "approved",
            "body": "Report-level conclusion for {{reportNumber}}. It is printed once, not under each sample.",
        },
        {
            "key": "comment.oil-a",
            "language": "en",
            "versionLabel": "1.0.0",
            "scope": "sample",
            "category": "comment",
            "effectiveFrom": "2026-01-01",
            "effectiveTo": None,
            "approvalStatus": "approved",
            "body": "Demo comment bound to sample {{sampleId}} ({{sampleCode}}). Description echo: {{sampleDescription}}.",
        },
        {
            "key": "comment.oil-b",
            "language": "en",
            "versionLabel": "1.0.0",
            "scope": "sample",
            "category": "comment",
            "effectiveFrom": "2026-01-01",
            "effectiveTo": None,
            "approvalStatus": "approved",
            "body": "Second sample comment for {{sampleId}} only. It must not be printed on other samples.",
        },
        {
            "key": "conclusion.oil-a",
            "language": "en",
            "versionLabel": "1.0.0",
            "scope": "sample",
            "category": "conclusion",
            "effectiveFrom": "2026-01-01",
            "effectiveTo": None,
            "approvalStatus": "approved",
            "body": "Demo conclusion for sample {{sampleId}}: fictional screen complete for the listed analytes.",
        },
        {
            "key": "conclusion.oil-b",
            "language": "en",
            "versionLabel": "1.0.0",
            "scope": "sample",
            "category": "conclusion",
            "effectiveFrom": "2026-01-01",
            "effectiveTo": None,
            "approvalStatus": "approved",
            "body": "Demo conclusion for sample {{sampleId}}: one fictional limit is exceeded in this synthetic set.",
        },
        {
            "key": "result.note.pb",
            "language": "en",
            "versionLabel": "1.0.0",
            "scope": "result",
            "category": "resultNote",
            "effectiveFrom": "2026-01-01",
            "effectiveTo": None,
            "approvalStatus": "approved",
            "body": "Result note for {{analyteName}} on sample {{sampleId}}. Displayed result: {{resultDisplay}} {{unit}}.",
        },
        {
            "key": "method.note.01",
            "language": "en",
            "versionLabel": "1.0.0",
            "scope": "method",
            "category": "methodNote",
            "effectiveFrom": "2026-01-01",
            "effectiveTo": None,
            "approvalStatus": "approved",
            "body": "Method note for {{methodCode}}: {{methodName}}. Citation: {{referenceCitation}}",
        },
        {
            "key": "approval.demo-block",
            "language": "en",
            "versionLabel": "1.0.0",
            "scope": "section",
            "category": "sectionIntro",
            "effectiveFrom": "2026-01-01",
            "effectiveTo": None,
            "approvalStatus": "approved",
            "body": "Visual label for {{signatoryName}}, {{signatoryRole}}. {{signatoryStatement}} Placeholder resolution for this block is limited to the approval section.",
        },
        {
            "key": "comment.stress",
            "language": "en",
            "versionLabel": "1.0.0",
            "scope": "sample",
            "category": "comment",
            "effectiveFrom": "2026-01-01",
            "effectiveTo": None,
            "approvalStatus": "approved",
            "body": "Short stress comment attached only to sample {{sampleId}}.",
        },
        {
            "key": "comment.stress.long",
            "language": "en",
            "versionLabel": "1.0.0",
            "scope": "sample",
            "category": "comment",
            "effectiveFrom": "2026-01-01",
            "effectiveTo": None,
            "approvalStatus": "approved",
            "body": long_body,
        },
    ]
    return {"catalogVersion": "2026.1", "entries": entries}


def signatories():
    return {
        "catalogVersion": "2026.1",
        "signatories": [
            {
                "signatoryId": "sig-demo-reviewer",
                "displayName": "R. Quinn",
                "roleTitle": "Synthetic layout reviewer",
                "statement": "Approves the demo layout only. No laboratory authorization is claimed.",
                "assetKey": "demo-mark",
            }
        ],
    }


def column(key, label):
    return {"key": key, "label": label, "visible": True}


def template(template_id, name, samples_per_page, chunk):
    result_columns = [
        column("analyte", "Analyte"),
        column("result", "Result"),
        column("qualifier", "Qualifier"),
        column("unit", "Unit"),
        column("lod", "LOD"),
        column("loq", "LOQ"),
        column("method", "Method"),
    ]
    sections = [
        {"instanceId": "report-metadata", "type": "report-metadata", "title": "Report details", "order": 10, "enabled": True, "columns": [], "subsections": [], "textKeys": []},
        {"instanceId": "sample-summary", "type": "sample-summary", "title": "Sample summary", "order": 20, "enabled": True, "columns": [], "subsections": [], "textKeys": []},
        {
            "instanceId": "analytical-results",
            "type": "analytical-results",
            "title": "Analytical results",
            "order": 30,
            "enabled": True,
            "columns": result_columns,
            "subsections": [
                {"id": "metals", "title": "Metals", "order": 10, "enabled": True, "analyteGroup": "metals"},
                {"id": "screen", "title": "Screen", "order": 20, "enabled": True, "analyteGroup": "screen"},
            ],
            "textKeys": [],
        },
        {"instanceId": "comments", "type": "comments", "title": "Comments", "order": 40, "enabled": True, "columns": [], "subsections": [], "textKeys": []},
        {"instanceId": "conclusions", "type": "conclusions", "title": "Conclusions", "order": 50, "enabled": True, "columns": [], "subsections": [], "textKeys": []},
        {"instanceId": "method-references", "type": "method-references", "title": "Method references", "order": 60, "enabled": True, "columns": [], "subsections": [], "textKeys": []},
        {"instanceId": "disclaimers", "type": "disclaimers", "title": "Disclaimer", "order": 70, "enabled": True, "columns": [], "subsections": [], "textKeys": ["disclaimer.scope"]},
        {"instanceId": "approval-signature", "type": "approval-signature", "title": "Layout label", "order": 80, "enabled": True, "columns": [], "subsections": [], "textKeys": ["approval.demo-block"]},
    ]
    return {
        "templateId": template_id,
        "name": name,
        "version": 1,
        "language": "en",
        "samplesPerPage": samples_per_page,
        "resultRowsPerChunk": chunk,
        "paper": "A4",
        "orientation": "portrait",
        "marginTopMm": 24,
        "marginBottomMm": 16,
        "marginSideMm": 12,
        "signatoryId": "sig-demo-reviewer",
        "jurisdiction": "FICTION-ZONE-7",
        "updatedUtc": UPDATED,
        "sections": sections,
    }


def dump(path: Path, payload) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


def sql_str(value) -> str:
    if value is None:
        return "NULL"
    return "N'" + str(value).replace("'", "''") + "'"


def sql_date(value) -> str:
    if value is None:
        return "NULL"
    return f"'{value}'"


def sql_num(value) -> str:
    if value is None:
        return "NULL"
    return str(value)


def sql_bool(value) -> str:
    return "1" if value else "0"


def emit_sql(reports, catalog_texts, catalog_limits) -> str:
    lines = [
        "-- Generated by tools/build_seed.py. Synthetic rows only.",
        "SET NOCOUNT ON;",
        "INSERT INTO lab.Tenant (TenantId, Name) VALUES (N'demo-tenant', N'Northline demonstration tenant');",
        "INSERT INTO lab.Tenant (TenantId, Name) VALUES (N'other-tenant', N'Other demonstration tenant');",
        "INSERT INTO lab.Principal (PrincipalId, TenantId, DisplayName) VALUES (N'demo-reader', N'demo-tenant', N'Demo reader');",
        "INSERT INTO lab.Principal (PrincipalId, TenantId, DisplayName) VALUES (N'other-reader', N'other-tenant', N'Other reader');",
        "",
    ]
    analytes = {}
    for doc in reports:
        for sample_row in doc["samples"]:
            for res in sample_row["results"]:
                analytes.setdefault(res["analyteCode"], (res["analyteName"], res["analyteGroup"]))
    for code, (name, group) in sorted(analytes.items()):
        lines.append(
            "INSERT INTO lab.Analyte (AnalyteCode, AnalyteName, AnalyteGroup) VALUES ("
            f"{sql_str(code)}, {sql_str(name)}, {sql_str(group)});"
        )
    lines.append("")
    for method in METHODS:
        lines.append(
            "INSERT INTO lab.MethodReference (MethodCode, MethodName, Summary, ReferenceCitation, VersionLabel) VALUES ("
            f"{sql_str(method['methodCode'])}, {sql_str(method['methodName'])}, {sql_str(method['summary'])}, "
            f"{sql_str(method['referenceCitation'])}, {sql_str(method['versionLabel'])});"
        )
    lines.append("")
    for doc in reports:
        lines.append(
            "INSERT INTO lab.Report (TenantId, ReportId, ReportNumber, SchemaVersion, Title, ReportDate, Matrix, Commodity, ClientName, ClientReference, Origin, OrderedBy, SubmittedBy, SourceFeed, SourceRevision) VALUES ("
            f"N'demo-tenant', {sql_str(doc['reportId'])}, {sql_str(doc['reportNumber'])}, N'1.0', {sql_str(doc['title'])}, "
            f"'{doc['reportDate']}', {sql_str(doc['matrix'])}, {sql_str(doc['commodity'])}, {sql_str(doc['clientName'])}, "
            f"{sql_str(doc['clientReference'])}, {sql_str(doc['origin'])}, {sql_str(doc['orderedBy'])}, {sql_str(doc['submittedBy'])}, "
            f"{sql_str(doc['sourceFeed'])}, {sql_str(doc['sourceRevision'])});"
        )
        lines.append(
            f"INSERT INTO lab.ReportAccess (TenantId, ReportId, PrincipalId, AccessLevel) VALUES (N'demo-tenant', {sql_str(doc['reportId'])}, N'demo-reader', N'reader');"
        )
        for sample_row in doc["samples"]:
            lines.append(
                "INSERT INTO lab.Sample (TenantId, SampleId, ReportId, SampleCode, Ordinal, Description, Matrix, Commodity, SamplingDate, ReceivedDate, ReceptionTemperature, BabyFood, Organic) VALUES ("
                f"N'demo-tenant', {sql_str(sample_row['sampleId'])}, {sql_str(doc['reportId'])}, {sql_str(sample_row['sampleCode'])}, {sample_row['ordinal']}, "
                f"{sql_str(sample_row['description'])}, {sql_str(sample_row['matrix'])}, {sql_str(sample_row['commodity'])}, "
                f"{sql_date(sample_row['samplingDate'])}, {sql_date(sample_row['receivedDate'])}, {sql_str(sample_row['receptionTemperature'])}, "
                f"{sql_bool(sample_row['babyFood'])}, {sql_bool(sample_row['organic'])});"
            )
            for res in sample_row["results"]:
                lines.append(
                    "INSERT INTO lab.Result (TenantId, ResultId, SampleId, AnalyteCode, Ordinal, ResultKind, NumericValue, Qualifier, Lod, Loq, Unit, MethodCode) VALUES ("
                    f"N'demo-tenant', {sql_str(res['resultId'])}, {sql_str(res['sampleId'])}, {sql_str(res['analyteCode'])}, {res['ordinal']}, "
                    f"{sql_str(res['kind'])}, {sql_num(res['numericValue'])}, {sql_str(res['qualifier'])}, {sql_num(res['lod'])}, {sql_num(res['loq'])}, "
                    f"{sql_str(res['unit'])}, {sql_str(res['methodCode'])});"
                )
        for binding in doc["textBindings"]:
            lines.append(
                "INSERT INTO lab.TextBinding (TenantId, BindingId, ReportId, TextKey, LanguageCode, Scope, SampleId, ResultId, MethodCode) VALUES ("
                f"N'demo-tenant', {sql_str(binding['bindingId'])}, {sql_str(doc['reportId'])}, {sql_str(binding['textKey'])}, N'en', "
                f"{sql_str(binding['scope'])}, {sql_str(binding['sampleId'])}, {sql_str(binding['resultId'])}, {sql_str(binding['methodCode'])});"
            )
        lines.append("")
    lines.append(
        "INSERT INTO lab.Report (TenantId, ReportId, ReportNumber, SchemaVersion, Title, ReportDate, Matrix, Commodity, ClientName, ClientReference, Origin, OrderedBy, SubmittedBy, SourceFeed, SourceRevision) "
        "VALUES (N'other-tenant', N'RPT-OTHER-0001', N'OTHER-AR-1', N'1.0', N'Other tenant synthetic report', '2026-04-15', N'oil', N'refined-demo-oil', N'Other Cooperative', NULL, NULL, NULL, NULL, N'report-snapshot-v1', N'sql-other-1');"
    )
    lines.append(
        "INSERT INTO lab.ReportAccess (TenantId, ReportId, PrincipalId, AccessLevel) VALUES (N'other-tenant', N'RPT-OTHER-0001', N'other-reader', N'reader');"
    )
    lines.append(
        "INSERT INTO lab.Sample (TenantId, SampleId, ReportId, SampleCode, Ordinal, Description, Matrix, Commodity, SamplingDate, ReceivedDate, ReceptionTemperature, BabyFood, Organic) "
        "VALUES (N'other-tenant', N'OTHER-SMP-01', N'RPT-OTHER-0001', N'OTHER-SMP-01', 1, N'Row that demo-reader must not receive.', N'oil', N'refined-demo-oil', NULL, NULL, NULL, 0, 0);"
    )
    lines.append(
        "INSERT INTO lab.Result (TenantId, ResultId, SampleId, AnalyteCode, Ordinal, ResultKind, NumericValue, Qualifier, Lod, Loq, Unit, MethodCode) "
        "VALUES (N'other-tenant', N'OTHER-RES-01', N'OTHER-SMP-01', N'DM-PB', 1, N'numeric', 0.01, NULL, 0.01, 0.05, N'mg/kg', N'DEMO-MET-01');"
    )
    lines.append("")
    for limit in catalog_limits["limits"]:
        lines.append(
            "INSERT INTO lab.MrlLimit (MrlId, AnalyteCode, Jurisdiction, Matrix, Commodity, Unit, LimitValue, EffectiveFrom, EffectiveTo, SourceName, VersionLabel, Fictional) VALUES ("
            f"{sql_str(limit['mrlId'])}, {sql_str(limit['analyteCode'])}, {sql_str(limit['jurisdiction'])}, {sql_str(limit['matrix'])}, "
            f"{sql_str(limit['commodity'])}, {sql_str(limit['unit'])}, {sql_num(limit['limitValue'])}, {sql_date(limit['effectiveFrom'])}, "
            f"{sql_date(limit['effectiveTo'])}, {sql_str(limit['sourceName'])}, {sql_str(limit['versionLabel'])}, 1);"
        )
    lines.append("")
    for entry in catalog_texts["entries"]:
        lines.append(
            "INSERT INTO lab.TextCatalog (TextKey, LanguageCode, VersionLabel, Scope, Category, EffectiveFrom, EffectiveTo, ApprovalStatus, Body) VALUES ("
            f"{sql_str(entry['key'])}, {sql_str(entry['language'])}, {sql_str(entry['versionLabel'])}, {sql_str(entry['scope'])}, "
            f"{sql_str(entry['category'])}, {sql_date(entry['effectiveFrom'])}, {sql_date(entry['effectiveTo'])}, {sql_str(entry['approvalStatus'])}, {sql_str(entry['body'])});"
        )
    lines.append("")
    lines.append(
        "INSERT INTO lab.Signatory (SignatoryId, DisplayName, RoleTitle, Statement, AssetKey) VALUES ("
        "N'sig-demo-reviewer', N'R. Quinn', N'Synthetic layout reviewer', "
        "N'Approves the demo layout only. No laboratory authorization is claimed.', N'demo-mark');"
    )
    lines.append("")
    return "\n".join(lines) + "\n"


def main() -> None:
    normal = normal_report()
    stress = stress_report()
    dump(SEED / "feeds" / "report-snapshot-v1" / "demo-tenant" / "RPT-DEMO-0001.json", normal)
    dump(SEED / "feeds" / "report-snapshot-v1" / "demo-tenant" / "RPT-DEMO-STRESS.json", stress)
    dump(SEED / "catalogs" / "mrl.json", limits())
    dump(SEED / "catalogs" / "texts.json", texts())
    dump(SEED / "catalogs" / "signatories.json", signatories())
    dump(SEED / "templates" / "analytical-default.json", template("analytical-default", "Analytical demonstration", 2, 16))
    dump(SEED / "templates" / "analytical-stress.json", template("analytical-stress", "Stress demonstration", 2, 12))
    sql = emit_sql([normal, stress], texts(), limits())
    (SQL / "002_seed.sql").write_text(sql, encoding="utf-8")
    print(f"wrote seed and {sql.count(chr(10))} SQL lines")


if __name__ == "__main__":
    main()
