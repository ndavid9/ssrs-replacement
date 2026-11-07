/* Turns a Nordic layout model into preview HTML. Markers stay distinct on purpose. */
(function () {
  const SNAP = () => window.NordicSnapshot;
  const LOGIC = () => window.NordicLogic;

  function esc(value) {
    return String(value ?? "")
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;");
  }

  function formatNumber(value) {
    if (typeof value !== "number" || !isFinite(value)) return esc(value);
    return esc(String(Math.round(value * 1000) / 1000));
  }

  function renderPreview(model) {
    const gap = `<aside class="sim-gap"><strong>Simulated pagination.</strong> Sample groups and result rows are sliced here, and headers repeat. This is not a print preview and not a PDF. Physical page breaks stay with the report desk.</aside>`;
    if (model.withheld) {
      return `<div class="withheld" data-withheld><p class="mock-ribbon">MOCK / NOT FOR RELEASE</p><p>Preview withheld. ${esc(model.withheld)}</p></div>${gap}`;
    }
    if (!model.pages.length) {
      return `<div class="withheld" data-withheld><p>Nothing to place on a page for this slice.</p></div>${gap}`;
    }
    return model.pages.map((page) => renderPage(page, model)).join("") + gap;
  }

  function renderPage(page, model) {
    const report = SNAP().report;
    const parts = [];
    parts.push(`<p class="mock-ribbon">MOCK / NOT FOR RELEASE</p>`);
    parts.push(`<p class="sim-flag" data-sim>${esc(page.banner.sim)}</p>`);
    if (page.banner.detail) parts.push(`<p class="sim-detail">${esc(page.banner.detail)}</p>`);
    parts.push(`<p class="run-head">${esc(report.reportNumber)} · ${esc(report.issuer)} · fictional layout</p>`);
    if (page.isFirst && model.config.domains.report) parts.push(reportBlock(model));
    page.placed.reportComments.forEach((body) => {
      parts.push(`<section class="scope-note" data-note-scope="report"><h3>Report comment</h3><p>${esc(body)}</p></section>`);
    });
    page.blocks.forEach((block) => parts.push(renderBlock(block, page, model)));
    if (page.placed.footnotes.length) {
      parts.push(`<ol class="footnotes">${page.placed.footnotes.map((note) => `<li data-footnote="${esc(note.methodCode)}" value="${note.n}">${esc(note.text)}</li>`).join("")}</ol>`);
    }
    page.placed.reportConclusions.forEach((body) => {
      parts.push(`<section class="scope-note conclusion" data-note-scope="conclusion"><h3>Report conclusion</h3><p>${esc(body)}</p></section>`);
    });
    if (page.signoff) parts.push(signoffBlock());
    if (page.signoffWithheld) {
      parts.push(`<p class="signoff-withheld">Sign-off withheld until blocking layout issues are resolved. It is not printed on an unsafe configuration.</p>`);
    }
    parts.push(legend());
    return `<article class="sim-page" data-page="${page.number}" data-pages="${page.total}" data-samples="${esc(page.sampleIds.join(","))}" data-signoff="${page.signoff ? "true" : "false"}">${parts.join("")}</article>`;
  }

  function reportBlock(model) {
    const report = SNAP().report;
    const matrix = SNAP().matrices[model.config.matrix];
    const rows = [
      ["Report number", report.reportNumber],
      ["Title", report.title],
      ["Client", report.client],
      ["Site", report.site],
      ["Issued", report.issued],
      ["Issuer", report.issuer],
      ["Layout slice", matrix ? matrix.label : model.config.matrix]
    ];
    return `<section class="report-block"><h3>Report</h3><dl class="kv">${rows.map(([label, value]) => `<dt>${esc(label)}</dt><dd>${esc(value)}</dd>`).join("")}</dl><p class="muted">One snapshot feeds every preset. This block is the report domain, not a separate document.</p></section>`;
  }

  function signoffBlock() {
    const block = SNAP().signoff;
    return `<footer class="signoff" data-signoff><p class="sim-flag">Report sign-off · last simulated page only · not a signature</p><h3>${esc(block.heading)}</h3><p><strong>${esc(block.name)}</strong> · ${esc(block.role)}</p><p>${esc(block.statement)}</p></footer>`;
  }

  function legend() {
    return `<p class="legend"><span class="mark-nd">not detected</span> is below a reporting limit. <span class="mark-missing">not reported</span> means the slot is empty. <span class="mark-unacc">Not accredited</span> is a method mark and still shows the result. <span class="mark-layout">hidden by layout</span> means a domain is off.</p>`;
  }

  function renderBlock(block, page, model) {
    if (block.mode === "matrix") return renderMatrix(block, page, model);
    return renderVertical(block, page, model);
  }

  function renderVertical(block, page, model) {
    const sample = block.sample;
    const cols = model.effectiveColumns;
    const span = 1 + cols.length;
    const head = cols.map((key) => `<th>${esc(LOGIC().COLUMN_LABELS[key])}</th>`).join("");
    const body = block.atoms.map((atom) => renderAtom(atom, [sample], page, model, cols, span, "vertical")).join("");
    const caption = sampleCaption(sample, block.continued);
    return `<section class="sample-block">${caption}<table><thead><tr><th>Parameter</th>${head}</tr></thead><tbody>${body}</tbody></table>${notesAfter(page, [sample])}</section>`;
  }

  function renderMatrix(block, page, model) {
    const samples = block.samples;
    const shared = model.sharedColumns;
    const perSample = perSampleKeys(model);
    const span = 1 + shared.length + samples.length * Math.max(1, perSample.length);
    const top = [`<th rowspan="2">Parameter</th>`]
      .concat(shared.map((key) => `<th rowspan="2">${esc(LOGIC().COLUMN_LABELS[key])}</th>`))
      .concat(samples.map((sample) => `<th colspan="${Math.max(1, perSample.length)}">${esc(sample.id)}</th>`))
      .join("");
    const sub = samples.map(() => {
      if (!perSample.length) return `<th>Sample</th>`;
      return perSample.map((key) => `<th>${esc(LOGIC().COLUMN_LABELS[key])}</th>`).join("");
    }).join("");
    const body = block.atoms.map((atom) => renderAtom(atom, samples, page, model, shared, span, "matrix", perSample)).join("");
    const continued = block.continued ? " · continued" : "";
    return `<section class="sample-block"><p class="sample-caption">Sample columns ${esc(samples[0].id)} to ${esc(samples[samples.length - 1].id)}${continued}. Descriptions repeat in the column header.</p><table><thead><tr>${top}</tr><tr>${sub}</tr></thead><tbody>${body}</tbody></table>${sampleDetails(samples)}${notesAfter(page, samples)}</section>`;
  }

  function perSampleKeys(model) {
    const keys = [];
    if (model.effectiveColumns.indexOf("result") !== -1) keys.push("result");
    if (model.config.mode === "matrix" && model.effectiveColumns.indexOf("evaluation") !== -1) keys.push("evaluation");
    return keys;
  }

  function sampleCaption(sample, continued) {
    return `<h3 class="sample-caption">${continued ? "Continued · " : ""}${esc(sample.id)} · ${esc(sample.description)}</h3><p class="muted">${esc(sample.detail)} · taken ${esc(sample.taken)} · ${esc(sample.matrix)}</p>`;
  }

  function sampleDetails(samples) {
    return `<ul class="sample-details">${samples.map((sample) => `<li><strong>${esc(sample.id)}</strong> ${esc(sample.description)} · ${esc(sample.detail)}</li>`).join("")}</ul>`;
  }

  function renderAtom(atom, samples, page, model, leadingCols, span, mode, perSample) {
    if (atom.type === "group") {
      const note = footnoteMark(page, atom.methodCode, atom.accredited);
      const cont = atom.continued ? " (continued)" : "";
      return `<tr class="group-row"><th colspan="${span}">${esc(atom.title)}${cont} · ${esc(atom.methodCode)} ${esc(atom.methodName)}${note}</th></tr>`;
    }
    const row = atom.row;
    const cells = [`<th>${parameterLabel(row)}</th>`];
    if (mode === "vertical") {
      model.effectiveColumns.forEach((key) => cells.push(`<td>${cellFor(key, row, samples[0], page, model, "vertical")}</td>`));
    } else {
      leadingCols.forEach((key) => cells.push(`<td>${cellFor(key, row, samples[0], page, model, "matrix")}</td>`));
      samples.forEach((sample) => {
        if (!perSample.length) {
          cells.push(`<td><span class="mark-layout" data-marker="layout">hidden by layout</span></td>`);
          return;
        }
        perSample.forEach((key) => cells.push(`<td>${cellFor(key, row, sample, page, model, "matrix")}</td>`));
      });
    }
    const inline = mode === "vertical" ? verticalResultNote(atom, samples[0], span) : "";
    return `<tr data-parameter="${esc(row.code)}">${cells.join("")}</tr>${inline}`;
  }

  function parameterLabel(row) {
    const calc = row.calculated ? ` <small class="calc">calculated</small>` : "";
    return `${esc(row.name)} <span class="code">${esc(row.code)}</span>${calc}`;
  }

  function verticalResultNote(atom, sample, span) {
    const notes = (atom.resultNotes || []).filter((note) => note.sampleId === sample.id);
    if (!notes.length) return "";
    return notes.map((note) => `<tr class="result-note"><td colspan="${span}" data-note-scope="result">${esc(note.body)}</td></tr>`).join("");
  }

  function cellFor(key, row, sample, page, model, mode) {
    if (key === "result") return resultHtml(row, sample, model, mode);
    if (key === "unit") return plainOrMissing(row.unit);
    if (key === "loq") return numberOrMissing(row.loq);
    if (key === "lor") return numberOrMissing(row.lor);
    if (key === "mu") return muHtml(row, sample);
    if (key === "method") return `<span class="method-code">${esc(row.methodCode)}</span>${footnoteMark(page, row.methodCode, row.accredited)}`;
    if (key === "reference") return row.reference ? esc(row.reference) : `<span class="mark-na">none</span>`;
    if (key === "accreditation") {
      return row.accredited
        ? `<span class="mark-na">Accredited</span>`
        : `<span class="mark-unacc" data-marker="unaccredited">Not accredited</span>`;
    }
    if (key === "limit") return limitHtml(row, model);
    if (key === "evaluation") return evaluationHtml(row, sample, model, mode);
    return "";
  }

  function resultHtml(row, sample, model, mode) {
    if (!model.config.domains.result) return `<span class="mark-layout" data-marker="layout">hidden by layout</span>`;
    const cell = row.results[sample.id];
    const muInline = mode === "matrix" && model.effectiveColumns.indexOf("mu") !== -1
      ? ` ${muHtml(row, sample)}`
      : "";
    const note = mode === "matrix"
      ? matrixResultNote(row, sample, model)
      : "";
    if (!cell || cell.status === "missing") {
      return `<span class="mark-missing" data-marker="missing">not reported</span>${note}`;
    }
    if (cell.status === "notDetected") {
      const bound = row.lor != null ? row.lor : row.loq;
      const value = bound == null ? "not detected" : `&lt; ${formatNumber(bound)} <small>not detected</small>`;
      return `<span class="mark-nd" data-marker="nd">${value}</span>${muInline}${note}`;
    }
    return `<span data-marker="value">${formatNumber(cell.value)}</span>${muInline}${note}`;
  }

  function matrixResultNote(row, sample, model) {
    if (!model.config.domains.comment || model.config.commentScopes.indexOf("result") === -1) return "";
    const notes = SNAP().comments.filter((comment) => comment.scope === "result" && comment.sampleId === sample.id && comment.parameterCode === row.code);
    if (!notes.length) return "";
    return notes.map((note) => `<div class="cell-note" data-note-scope="result">${esc(note.body)}</div>`).join("");
  }

  function muHtml(row, sample) {
    const cell = row.results[sample.id];
    if (!cell || cell.status === "missing") return `<span class="mark-missing" data-marker="missing-mu">not reported</span>`;
    if (cell.status === "notDetected") return `<span class="mark-na">n/a</span>`;
    if (cell.mu == null) return `<span class="mark-missing" data-marker="missing-mu">not reported</span>`;
    return `<span class="mu">± ${formatNumber(cell.mu)}</span>`;
  }

  function limitHtml(row, model) {
    const match = LOGIC().matchedLimit(model.config, row);
    if (!match) return `<span class="mark-na">not applied</span>`;
    return `${formatNumber(match.limit)} ${esc(match.unit)}`;
  }

  function evaluationHtml(row, sample, model) {
    const evaluation = LOGIC().evaluateCell(model.config, row, sample.id);
    return `<span class="eval eval-${esc(evaluation.kind)}" data-eval="${esc(row.code)}" data-sample="${esc(sample.id)}">${esc(evaluation.label)}</span>`;
  }

  function footnoteMark(page, methodCode, accredited) {
    if (accredited !== false) return "";
    const note = page.placed.footnotes.find((item) => item.methodCode === methodCode);
    if (!note) return "";
    return ` <sup class="fn">†${note.n}</sup>`;
  }

  function plainOrMissing(value) {
    if (value == null || value === "") return `<span class="mark-missing" data-marker="missing">not reported</span>`;
    return esc(value);
  }

  function numberOrMissing(value) {
    if (value == null) return `<span class="mark-missing" data-marker="missing">not reported</span>`;
    return formatNumber(value);
  }

  function notesAfter(page, samples) {
    const ids = new Set(samples.map((sample) => sample.id));
    const testNotes = page.placed.testNotes.filter((note) => ids.has(note.sampleId));
    const sampleNotes = page.placed.sampleNotes.filter((note) => ids.has(note.sampleId));
    const empty = page.placed.emptySampleComments.filter((id) => ids.has(id));
    const conclusions = page.placed.sampleConclusions.filter((note) => ids.has(note.sampleId));
    const parts = [];
    testNotes.forEach((note) => {
      parts.push(`<p class="scope-note" data-note-scope="test"><strong>Test comment · ${esc(note.testCode)} · ${esc(note.sampleId)}</strong> ${esc(note.body)}</p>`);
    });
    sampleNotes.forEach((note) => {
      parts.push(`<p class="scope-note" data-note-scope="sample"><strong>Sample comment · ${esc(note.sampleId)}</strong> ${esc(note.body)}</p>`);
    });
    if (empty.length) {
      parts.push(`<p class="scope-note muted" data-note-scope="sample-empty">No sample comment on file for ${esc(empty.join(", "))}.</p>`);
    }
    conclusions.forEach((note) => {
      parts.push(`<p class="scope-note conclusion" data-note-scope="sample-conclusion"><strong>Sample conclusion · ${esc(note.sampleId)}</strong> ${esc(note.body)}</p>`);
    });
    return parts.join("");
  }

  function renderMap(model) {
    const stats = model.stats;
    const config = model.config;
    const handlerRows = Object.keys(model.handlers).map((key) => `<tr><th>${esc(key)}</th><td><code>${esc(model.handlers[key])}</code></td></tr>`).join("");
    const feedRows = Object.keys(LOGIC().FEEDS).map((key) => `<tr><th>${esc(LOGIC().DOMAIN_LABELS[key] || key)}</th><td><code>${esc(LOGIC().FEEDS[key])}</code></td></tr>`).join("");
    const scopes = `comments ${config.commentScopes.join(", ") || "none"}; conclusions ${config.conclusionScopes.join(", ") || "none"}`;
    return `
      <p>Every preset reads <code>${esc(SNAP().id)}</code> on <code>${esc(SNAP().feed)}</code>. The preview is this layout document applied to that snapshot.</p>
      <p><strong>${esc(stats.samples)}</strong> of ${esc(stats.available)} samples in this slice (Sample is 1..*). <strong>${esc(stats.tests)}</strong> tests. <strong>${esc(stats.parameters)}</strong> parameters. <strong>${esc(stats.slots)}</strong> result slots at 0..1 — ${esc(stats.missing)} not reported, ${esc(stats.notDetected)} not detected, ${esc(stats.numeric)} numeric. ${esc(stats.unaccreditedTests)} test${stats.unaccreditedTests === 1 ? "" : "s"} outside the fictional accreditation scope.</p>
      <p>Report is 1. Comments and conclusions are 0..* (${esc(scopes)}). MRL rows are 0..* and score only when unit, matrix, and jurisdiction all match. A miss is “not evaluated”, never a pass or fail.</p>
      <p>${esc(model.pages.length)} simulated page${model.pages.length === 1 ? "" : "s"} · ${esc(config.mode)} · ${esc(config.samplesPerPage)} sample${config.samplesPerPage === 1 ? "" : "s"} per page · ${esc(config.rowsPerPage)} row budget. ${esc(model.handlers.pagination)}. PDF is not produced here.</p>
      <table class="map-table"><tbody>${handlerRows}</tbody></table>
      <h3>Feeds</h3>
      <table class="map-table"><tbody>${feedRows}</tbody></table>`;
  }

  window.NordicRender = { renderPreview, renderMap };
})();
