/* Synthetic preview. Numbers are fictional and labelled as such. */
const MOCK = {
  report: {
    reportNumber: "SYN-AR-2041",
    title: "Synthetic residue screen",
    client: "Northline Pantry Cooperative",
    reportDate: "2026-04-15",
    matrix: "oil",
    jurisdiction: "FICTION-ZONE-7"
  },
  reportComments: ["Report comment for SYN-AR-2041. It is printed once."],
  reportConclusions: ["Report conclusion for SYN-AR-2041. It is not repeated under each sample."],
  samples: [
    {
      sampleId: "SYN-SMP-01",
      description: "Fictional refined oil portion A",
      matrix: "oil",
      received: "2026-04-02",
      comments: ["Demo comment bound only to SYN-SMP-01."],
      conclusions: ["Demo conclusion for SYN-SMP-01."],
      tests: [
        {
          testCode: "SYN-MET-01",
          methodName: "Fictional digest screen A",
          accredited: true,
          parameters: [
            param("DM-PB", "Fictional lead analogue", "mg/kg", 0.01, 0.05, { kind: "numeric", value: 0.4 }, { limit: 1.11, unit: "mg/kg" }),
            param("DM-CD", "Fictional cadmium analogue", "mg/kg", 0.01, 0.05, { kind: "numeric", value: 1.25 }, { limit: 2, unit: "g/kg" }),
            param("DM-CU", "Fictional copper analogue", "mg/kg", 0.01, 0.05, { kind: "nd", qualifier: "ND" }, { limit: 3.33, unit: "mg/kg" })
          ]
        },
        {
          testCode: "SYN-MET-09",
          methodName: "Fictional unaccredited screen",
          accredited: false,
          parameters: [
            param("DM-ZN", "Fictional zinc analogue", "mg/kg", 0.02, 0.05, { kind: "numeric", value: 0.1 }, { limit: 9.99, unit: "mg/kg" })
          ]
        }
      ]
    },
    {
      sampleId: "SYN-SMP-02",
      description: "Fictional water portion B",
      matrix: "water",
      received: "2026-04-03",
      comments: ["Second sample comment for SYN-SMP-02 only."],
      conclusions: ["Demo conclusion for SYN-SMP-02."],
      tests: [
        {
          testCode: "SYN-MET-02",
          methodName: "Fictional water screen B",
          accredited: true,
          parameters: [
            param("DM-PB", "Fictional lead analogue", "mg/kg", 0.01, 0.05, { kind: "belowLoq", loq: 0.05 }, { limit: 1.11, unit: "mg/kg" })
          ]
        }
      ]
    },
    {
      sampleId: "SYN-SMP-03",
      description: "Fictional oil portion C with no comment on file",
      matrix: "oil",
      received: "2026-04-04",
      comments: [],
      conclusions: [],
      tests: [
        {
          testCode: "SYN-MET-01",
          methodName: "Fictional digest screen A",
          accredited: true,
          parameters: [
            param("DM-FE", "Fictional iron analogue", "mg/kg", 0.05, 0.1, { kind: "notTested" }, null)
          ]
        }
      ]
    }
  ]
};

function param(code, name, unit, lod, loq, result, mrl) {
  return {
    code,
    name,
    unit,
    lod,
    loq,
    result: Object.assign({ qualifier: null, value: null, loq: null }, result),
    mrl: mrl ? { jurisdiction: "FICTION-ZONE-7", effective: "2026-01-01", limit: mrl.limit, unit: mrl.unit } : null
  };
}

function renderPreview(reportType) {
  const sections = [...reportType.sections].filter((section) => section.enabled).sort((a, b) => a.order - b.order);
  const samples = visibleSamples(reportType);
  const spp = clampPage(reportType.samplesPerPage);
  const groups = chunk(samples, spp);
  const notes = [];
  let footnote = 1;
  const methodNumbers = new Map();

  const parts = [];
  parts.push(`<p class="banner">SYNTHETIC DATA / NOT A VALID LABORATORY CERTIFICATE</p>`);
  parts.push(`<p class="banner-sub">${esc(reportType.name)} · mock preview · not a laboratory document</p>`);

  if (has(sections, "report")) {
    const section = find(sections, "report");
    parts.push(`<h3>${esc(section.title)}</h3>`);
    parts.push(fieldList(section, MOCK.report));
  }

  sections.filter((section) => section.domain === "comment" && section.scopes.includes("report")).forEach((section) => {
    parts.push(textBlock(section, MOCK.reportComments, "report SYN-AR-2041"));
  });
  sections.filter((section) => section.domain === "conclusion" && section.scopes.includes("report")).forEach((section) => {
    parts.push(textBlock(section, MOCK.reportConclusions, "report SYN-AR-2041"));
  });

  if (has(sections, "sample")) {
    groups.forEach((group, index) => {
      parts.push(`<p class="group">Sample group ${index + 1} of ${groups.length}. This group starts ${group.length} new sample${group.length === 1 ? "" : "s"} (maximum ${spp}).</p>`);
      group.forEach((sample) => {
        parts.push(`<article class="sample">`);
        parts.push(`<h3>Sample ${esc(sample.sampleId)}</h3>`);
        const sampleSection = find(sections, "sample");
        parts.push(fieldList(sampleSection, sample));
        parts.push(renderTests(sections, sample, notes, methodNumbers, () => footnote++));
        sections.filter((section) => section.domain === "comment" && section.scopes.includes("sample")).forEach((section) => {
          parts.push(textBlock(section, sample.comments, sample.sampleId));
        });
        sections.filter((section) => section.domain === "conclusion" && section.scopes.includes("sample")).forEach((section) => {
          parts.push(textBlock(section, sample.conclusions, sample.sampleId));
        });
        parts.push(`</article>`);
      });
    });
    if (samples.length === 0) {
      parts.push(`<p class="muted">No mock sample matches the matrix rule.</p>`);
    }
  }

  if (notes.length) {
    parts.push(`<h3>Footnotes</h3><ol class="notes">`);
    notes.forEach((note) => {
      parts.push(`<li value="${note.n}">${esc(note.text)}</li>`);
    });
    parts.push(`</ol>`);
  }

  return parts.join("");
}

function renderTests(sections, sample, notes, methodNumbers, nextFoot) {
  if (!has(sections, "test")) return "";
  const testSection = find(sections, "test");
  const html = [];
  sample.tests.forEach((test) => {
    if (testSection.rules.hideIfNotAccredited && !test.accredited) return;
    if (!methodNumbers.has(test.testCode)) methodNumbers.set(test.testCode, methodNumbers.size + 1);
    const methodNo = methodNumbers.get(test.testCode);
    let mark = "";
    const accreditation = find(sections, "accreditation");
    if (accreditation && !test.accredited && accreditation.accreditation && accreditation.accreditation.footnoteNonAccredited) {
      const n = nextFoot();
      const glyph = accreditation.accreditation.mark || "†";
      mark = ` <sup>${esc(glyph)}${n}</sup>`;
      notes.push({
        n,
        text: `${test.testCode} is not accredited in this synthetic catalog. The mark is a layout flag, not a certificate.`
      });
    }
    html.push(`<h4>${esc(testSection.title)} ${esc(test.testCode)}${mark}</h4>`);
    html.push(fieldList(testSection, {
      testCode: test.testCode,
      methodName: test.methodName,
      accredited: test.accredited ? "Yes" : "No"
    }));
    html.push(`<p class="muted">Method <sup>${methodNo}</sup> ${esc(test.methodName)}</p>`);
    if (has(sections, "parameter")) {
      html.push(renderParameters(sections, sample, test));
    }
  });
  return html.join("");
}

function renderParameters(sections, sample, test) {
  const parameterSection = find(sections, "parameter");
  const resultSection = find(sections, "result");
  const mrlSection = find(sections, "mrl");
  const rows = test.parameters.map((parameter) => {
    const cells = [];
    if (parameterSection) {
      visibleColumns(parameterSection).forEach((column) => cells.push(esc(showValue(parameter, column.key))));
    }
    if (resultSection) {
      visibleColumns(resultSection).forEach((column) => {
        const result = parameter.result;
        const value = column.key === "display" ? resultDisplay(result) : column.key === "qualifier" ? (result.qualifier || "—") : (result.kind === "numeric" || result.kind === "belowLoq" ? parameter.unit : "—");
        cells.push(esc(value));
      });
    }
    if (mrlSection) {
      const evaluation = evaluateMrl(parameter, mrlSection);
      visibleColumns(mrlSection).forEach((column) => {
        cells.push(esc(mrlCell(parameter, evaluation, column.key)));
      });
    }
    const resultNotes = [];
    sections.filter((section) => section.domain === "comment" && section.scopes.includes("result")).forEach(() => {
      if (parameter.code === "DM-PB" && sample.sampleId === "SYN-SMP-01") {
        resultNotes.push(`Result note for ${parameter.name} on ${sample.sampleId}.`);
      }
    });
    return `<tr>${cells.map((cell) => `<td>${cell}</td>`).join("")}</tr>` +
      (resultNotes.length ? `<tr><td colspan="${cells.length}"><span class="note">${esc(resultNotes[0])}</span></td></tr>` : "");
  });
  const heads = [];
  if (parameterSection) visibleColumns(parameterSection).forEach((column) => heads.push(column.label));
  if (resultSection) visibleColumns(resultSection).forEach((column) => heads.push(column.label));
  if (mrlSection) visibleColumns(mrlSection).forEach((column) => heads.push(column.label));
  if (!heads.length) return "";
  return `<table><thead><tr>${heads.map((head) => `<th>${esc(head)}</th>`).join("")}</tr></thead><tbody>${rows.join("")}</tbody></table>`;
}

function evaluateMrl(parameter, mrlSection) {
  const limit = parameter.mrl;
  const result = parameter.result;
  if (!limit) return { label: "No applicable fictional limit.", kind: "none" };
  if (mrlSection.mrl && mrlSection.mrl.jurisdiction && limit.jurisdiction !== mrlSection.mrl.jurisdiction) {
    return { label: "No applicable fictional limit for this jurisdiction.", kind: "none" };
  }
  const requireUnit = !mrlSection.mrl || mrlSection.mrl.requireUnitMatch;
  if (requireUnit && parameter.unit !== limit.unit) {
    return { label: "Not evaluated. Units differ, so this is not a pass or fail.", kind: "mismatch", limit };
  }
  if (result.kind === "numeric") {
    if (result.value <= limit.limit) return { label: "Within the fictional limit.", kind: "within", limit };
    return { label: "Exceeds the fictional limit.", kind: "exceeds", limit };
  }
  if (result.kind === "belowLoq") {
    return { label: "Not evaluated as a numeric pass. The result is below LOQ.", kind: "loq", limit };
  }
  return { label: "Not evaluated. The result is ND or not tested.", kind: "other", limit };
}

function mrlCell(parameter, evaluation, key) {
  if (key === "evaluation") return evaluation.label;
  if (!evaluation.limit && key !== "evaluation") return "—";
  if (key === "limit") return String(evaluation.limit.limit);
  if (key === "unit") return evaluation.limit.unit;
  if (key === "jurisdiction") return evaluation.limit.jurisdiction;
  if (key === "effective") return evaluation.limit.effective;
  return "—";
}

function resultDisplay(result) {
  if (result.kind === "numeric") return String(result.value);
  if (result.kind === "belowLoq") return "< " + result.loq;
  if (result.kind === "nd") return "ND";
  if (result.kind === "notTested") return "Not tested";
  return "—";
}

function visibleSamples(reportType) {
  const sampleSection = reportType.sections.find((section) => section.enabled && section.domain === "sample");
  const matrix = sampleSection && sampleSection.rules ? sampleSection.rules.matrixEquals.trim().toLowerCase() : "";
  if (!matrix) return MOCK.samples;
  return MOCK.samples.filter((sample) => sample.matrix === matrix);
}

function textBlock(section, lines, anchor) {
  if (section.rules.showWhenHasData && (!lines || lines.length === 0)) return "";
  const body = lines && lines.length
    ? lines.map((line) => `<p>${esc(line)}</p>`).join("")
    : `<p class="muted">No ${esc(section.title.toLowerCase())} on file for ${esc(anchor)}.</p>`;
  return `<h4>${esc(section.title)} · ${esc(anchor)}</h4>${body}`;
}

function fieldList(section, record) {
  if (!section) return "";
  const rows = visibleColumns(section).map((column) => {
    return `<div><dt>${esc(column.label)}</dt><dd>${esc(showValue(record, column.key))}</dd></div>`;
  });
  return `<dl class="fields">${rows.join("")}</dl>`;
}

function showValue(record, key) {
  const value = record[key];
  if (value === null || value === undefined || value === "") return "—";
  return String(value);
}

function visibleColumns(section) {
  return (section.columns || []).filter((column) => column.visible);
}

function has(sections, domain) {
  return sections.some((section) => section.domain === domain);
}

function find(sections, domain) {
  return sections.find((section) => section.domain === domain);
}

function chunk(items, size) {
  const groups = [];
  for (let index = 0; index < items.length; index += size) groups.push(items.slice(index, index + size));
  return groups.length ? groups : [[]];
}

function clampPage(value) {
  const number = Number(value);
  if (!Number.isInteger(number) || number < 1) return 1;
  return Math.min(number, 12);
}

function esc(value) {
  return String(value ?? "")
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}
