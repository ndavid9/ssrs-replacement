/* Declarative Nordic layout: one snapshot, many presets. No report HTML is stored here. */
(function () {
  const SCHEMA = "nordic-sandbox-1.0";
  const STORAGE_KEY = "northline-nordic-sandbox-v1";
  const FEEDS = {
    report: "report-snapshot-v1",
    sample: "report-snapshot-v1",
    test: "report-snapshot-v1",
    parameter: "report-snapshot-v1",
    result: "report-snapshot-v1",
    comment: "text-catalog-v1",
    conclusion: "text-catalog-v1",
    mrl: "mrl-fictional-v1",
    accreditation: "accreditation-catalog-v1"
  };
  const COLUMN_KEYS = ["result", "unit", "loq", "lor", "mu", "method", "reference", "accreditation", "limit", "evaluation"];
  const DOMAIN_KEYS = ["report", "sample", "test", "parameter", "result", "comment", "conclusion", "mrl", "accreditation"];
  const SCOPE_KEYS = ["report", "sample", "test", "result"];
  const COLUMN_LABELS = {
    result: "Result",
    unit: "Unit",
    loq: "LOQ",
    lor: "LOR",
    mu: "MU",
    method: "Method",
    reference: "Reference",
    accreditation: "Accreditation",
    limit: "Limit",
    evaluation: "Evaluation"
  };
  const DOMAIN_LABELS = {
    report: "Report",
    sample: "Sample",
    test: "Tests",
    parameter: "Parameters",
    result: "Results",
    comment: "Comments",
    conclusion: "Conclusions",
    mrl: "MRL",
    accreditation: "Accreditation"
  };

  function clone(value) {
    return JSON.parse(JSON.stringify(value));
  }

  function columns(on) {
    const out = {};
    COLUMN_KEYS.forEach((key) => {
      out[key] = on.indexOf(key) !== -1;
    });
    return out;
  }

  function domains(off) {
    const out = {};
    DOMAIN_KEYS.forEach((key) => {
      out[key] = !off || off.indexOf(key) === -1;
    });
    return out;
  }

  function layout(patch) {
    return {
      mode: patch.mode,
      matrix: patch.matrix,
      sampleCount: patch.sampleCount,
      samplesPerPage: patch.samplesPerPage,
      rowsPerPage: patch.rowsPerPage,
      groupParameters: patch.groupParameters !== false,
      domains: patch.domains || domains(patch.domainsOff),
      columns: columns(patch.columns),
      commentScopes: patch.commentScopes.slice(),
      conclusionScopes: patch.conclusionScopes.slice(),
      mrl: {
        jurisdiction: "FICTION-NORDIC",
        requireUnit: true,
        requireMatrix: true,
        requireJurisdiction: true
      },
      signoff: patch.signoff !== false
    };
  }

  const PRESETS = [
    {
      id: "harbour-vertical",
      name: "Harbour screen",
      blurb: "Single sample, vertical rows, MU, unit, LOR, method",
      config: layout({
        mode: "vertical",
        matrix: "water",
        sampleCount: 1,
        samplesPerPage: 1,
        rowsPerPage: 10,
        columns: ["result", "mu", "unit", "lor", "method"],
        domainsOff: ["mrl"],
        commentScopes: ["report", "result"],
        conclusionScopes: ["report"]
      })
    },
    {
      id: "basin-groups",
      name: "Basin groups",
      blurb: "Grouped tests, uncertainty, continuation, scoped notes",
      config: layout({
        mode: "vertical",
        matrix: "groundwater",
        sampleCount: 2,
        samplesPerPage: 1,
        rowsPerPage: 10,
        columns: ["result", "mu", "unit", "method"],
        domainsOff: ["mrl"],
        commentScopes: ["sample", "test"],
        conclusionScopes: ["report"]
      })
    },
    {
      id: "plot-matrix",
      name: "Plot comparison",
      blurb: "Samples as columns on shared parameter rows, more than four",
      config: layout({
        mode: "matrix",
        matrix: "soil",
        sampleCount: 8,
        samplesPerPage: 4,
        rowsPerPage: 6,
        columns: ["result", "unit", "method"],
        domainsOff: ["mrl"],
        commentScopes: ["sample", "result"],
        conclusionScopes: ["report"]
      })
    },
    {
      id: "material-footnotes",
      name: "Material footnotes",
      blurb: "LOQ, MU, method, and accreditation notes on the pages that show them",
      config: layout({
        mode: "vertical",
        matrix: "hardened",
        sampleCount: 1,
        samplesPerPage: 1,
        rowsPerPage: 8,
        columns: ["result", "unit", "loq", "mu", "method", "accreditation"],
        domainsOff: ["mrl"],
        commentScopes: ["test"],
        conclusionScopes: ["report"]
      })
    },
    {
      id: "flush-limits",
      name: "Flush check",
      blurb: "Reference text, quantification limit, and match-or-not-evaluated",
      config: layout({
        mode: "vertical",
        matrix: "distribution",
        sampleCount: 2,
        samplesPerPage: 1,
        rowsPerPage: 16,
        columns: ["result", "unit", "loq", "reference", "method", "limit", "evaluation"],
        domains: domains(),
        commentScopes: ["report", "sample", "result"],
        conclusionScopes: ["report"]
      })
    }
  ];

  function preset(id) {
    return PRESETS.find((item) => item.id === id) || PRESETS[0];
  }

  function configFromPreset(id) {
    const item = preset(id);
    const config = clone(item.config);
    config.presetId = item.id;
    return config;
  }

  function normalize(config) {
    const base = configFromPreset(config && config.presetId);
    const incoming = config || {};
    const next = Object.assign({}, base, incoming);
    next.presetId = incoming.presetId || base.presetId;
    next.domains = Object.assign({}, base.domains, incoming.domains || {});
    next.columns = Object.assign({}, base.columns, incoming.columns || {});
    next.mrl = Object.assign({}, base.mrl, incoming.mrl || {});
    next.commentScopes = Array.isArray(incoming.commentScopes) ? incoming.commentScopes.slice() : base.commentScopes.slice();
    next.conclusionScopes = Array.isArray(incoming.conclusionScopes) ? incoming.conclusionScopes.slice() : base.conclusionScopes.slice();
    return next;
  }

  function snapshot() {
    return window.NordicSnapshot;
  }

  function availableCount(matrix) {
    return snapshot().samples.filter((sample) => sample.matrix === matrix).length;
  }

  function issue(level, code, message) {
    return { level, code, message };
  }

  function validate(config) {
    const issues = [];
    const snap = snapshot();
    if (!snap.matrices[config.matrix]) {
      issues.push(issue("error", "matrix.unknown", "That matrix is not in the shared snapshot."));
      return issues;
    }
    const available = availableCount(config.matrix);
    if (available < 1) issues.push(issue("error", "matrix.empty", "The shared snapshot has no samples for this matrix."));
    if (!Number.isInteger(config.sampleCount) || config.sampleCount < 1 || config.sampleCount > available) {
      issues.push(issue("error", "sample.count", `Sample count must be a whole number from 1 to ${available}.`));
    }
    if (!Number.isInteger(config.samplesPerPage) || config.samplesPerPage < 1 || config.samplesPerPage > 8) {
      issues.push(issue("error", "page.samples", "Samples per page must be a whole number from 1 to 8."));
    }
    if (!Number.isInteger(config.rowsPerPage) || config.rowsPerPage < 4 || config.rowsPerPage > 20) {
      issues.push(issue("error", "page.rows", "Rows per simulated page must be a whole number from 4 to 20."));
    }
    if (config.mode !== "vertical" && config.mode !== "matrix") {
      issues.push(issue("error", "mode.invalid", "Layout mode must be vertical or matrix."));
    }
    const on = config.domains || {};
    if (!on.report && DOMAIN_KEYS.some((key) => key !== "report" && on[key])) {
      issues.push(issue("error", "report.missing", "Child domains are on while the Report domain is off."));
    }
    if (!on.sample) issues.push(issue("error", "sample.required", "Sample identity is off, so neither a stacked block nor sample columns can be drawn."));
    if (!on.parameter) issues.push(issue("error", "parameter.required", "Parameters are the row identity. The table cannot be built without them."));
    if (on.result && !on.parameter) issues.push(issue("error", "result.needs-parameter", "Results need a parameter parent."));
    if (on.mrl && !on.result) issues.push(issue("error", "mrl.needs-result", "Limit evaluation is on, but Results are off. A limit cannot be scored without a result."));
    if (on.mrl && !on.parameter) issues.push(issue("error", "mrl.needs-parameter", "Limits join to a parameter."));
    if (on.mrl && (!config.mrl.requireUnit || !config.mrl.requireMatrix || !config.mrl.requireJurisdiction)) {
      issues.push(issue("error", "mrl.unsafe", "Limit evaluation stays blocked unless unit, matrix, and jurisdiction must all match. This sandbox will not invent a pass or fail."));
    }
    if (on.mrl && !/^[A-Z0-9-]{3,40}$/.test(config.mrl.jurisdiction || "")) {
      issues.push(issue("error", "mrl.jurisdiction", "Choose a jurisdiction token such as FICTION-NORDIC."));
    }
    if (config.groupParameters && !on.test) {
      issues.push(issue("error", "group.needs-test", "Parameter grouping follows tests, and Tests are off."));
    }
    if (on.accreditation && !on.test) {
      issues.push(issue("error", "accreditation.needs-test", "Accreditation marks hang off a test or method, and Tests are off."));
    }
    if (config.signoff && !on.report) {
      issues.push(issue("error", "signoff.needs-report", "Sign-off is report end-matter. Turn the Report domain on, or turn sign-off off."));
    }
    checkScopes(issues, config, "comment", "Comments");
    checkScopes(issues, config, "conclusion", "Conclusions");
    if (config.columns.accreditation && !on.accreditation) {
      issues.push(issue("warning", "column.accreditation", "The accreditation column is selected, but the Accreditation domain is off, so the column is omitted."));
    }
    if ((config.columns.limit || config.columns.evaluation) && !on.mrl) {
      issues.push(issue("warning", "column.mrl", "Limit columns are selected, but the MRL domain is off, so those columns are omitted."));
    }
    if (config.columns.method && !on.test) {
      issues.push(issue("warning", "column.method", "The method column is selected, but Tests are off, so the column is omitted."));
    }
    if (on.mrl && !config.columns.limit && !config.columns.evaluation) {
      issues.push(issue("warning", "mrl.hidden", "The MRL domain is on, but the limit and evaluation columns are hidden."));
    }
    if (on.result && !config.columns.result) {
      issues.push(issue("warning", "column.result", "Results are in the layout, but the result column is hidden."));
    }
    if (!on.result && !on.mrl) {
      issues.push(issue("warning", "result.hidden", "Results are off, so value cells stay blank rather than showing snapshot numbers."));
    }
    if (Number.isInteger(config.samplesPerPage) && config.samplesPerPage > 4) {
      issues.push(issue("warning", "spp.stress", "More than four samples on a page is a stress case. Headers still repeat. The sheet scrolls sideways in this preview."));
    }
    if (Number.isInteger(config.samplesPerPage) && Number.isInteger(config.sampleCount) && config.samplesPerPage > config.sampleCount) {
      issues.push(issue("warning", "spp.unused", "Samples per page is higher than the number of samples in this slice. Empty columns are not invented."));
    }
    if (config.mode === "matrix" && config.sampleCount < 2) {
      issues.push(issue("warning", "matrix.single", "Matrix layout with one sample is a single column. Vertical mode is the closer match for that slice."));
    }
    return issues;
  }

  function checkScopes(issues, config, domain, label) {
    if (!config.domains[domain]) return;
    const scopes = domain === "comment" ? config.commentScopes : config.conclusionScopes;
    const code = domain === "comment" ? "comment" : "conclusion";
    if (!scopes.length) {
      issues.push(issue("error", code + ".scope", `${label} are on, but no scope is selected.`));
      return;
    }
    scopes.forEach((scope) => {
      if (SCOPE_KEYS.indexOf(scope) === -1) {
        issues.push(issue("error", code + ".scope", `${label} include an unknown scope.`));
        return;
      }
      const parent = scope === "report" ? "report" : scope;
      if (!config.domains[parent]) {
        issues.push(issue("error", code + ".parent", `${label} are scoped to ${scope}, but that parent domain is off.`));
      }
    });
  }

  function blocking(issues) {
    return issues.some((item) => item.level === "error");
  }

  function identityBlocked(issues) {
    const codes = ["matrix.unknown", "matrix.empty", "sample.count", "page.samples", "page.rows", "mode.invalid", "sample.required", "parameter.required", "report.missing", "group.needs-test"];
    return issues.some((item) => item.level === "error" && codes.indexOf(item.code) !== -1);
  }

  function selectSamples(config) {
    return snapshot().samples.filter((sample) => sample.matrix === config.matrix).slice(0, config.sampleCount);
  }

  function flattenRows(config) {
    const matrix = snapshot().matrices[config.matrix];
    if (!matrix) return [];
    const rows = [];
    matrix.tests.forEach((group) => {
      group.parameters.forEach((parameter) => {
        rows.push({
          code: parameter.code,
          name: parameter.name,
          unit: parameter.unit,
          lor: parameter.lor,
          loq: parameter.loq,
          reference: parameter.reference,
          calculated: parameter.calculated,
          results: parameter.results,
          testCode: group.testCode,
          testTitle: group.title,
          methodCode: group.methodCode,
          methodName: group.methodName,
          accredited: group.accredited
        });
      });
    });
    return rows;
  }

  function buildAtoms(rows, config) {
    const atoms = [];
    let last = null;
    const grouping = Boolean(config.groupParameters && config.domains.test);
    rows.forEach((row, dataIndex) => {
      if (grouping && row.testCode !== last) {
        atoms.push({
          type: "group",
          testCode: row.testCode,
          title: row.testTitle,
          methodCode: row.methodCode,
          methodName: row.methodName,
          accredited: row.accredited,
          continued: false
        });
        last = row.testCode;
      }
      atoms.push({ type: "data", row, dataIndex, resultNotes: [] });
    });
    return atoms;
  }

  function takeAtoms(atoms, start, room, config) {
    if (room <= 0 || start >= atoms.length) return { atoms: [], next: start, used: 0 };
    const grouping = Boolean(config.groupParameters && config.domains.test);
    const out = [];
    let index = start;
    let used = 0;
    const needsHeader = grouping && atoms[index] && atoms[index].type === "data";
    if (needsHeader && room < 2) return { atoms: [], next: start, used: 0 };
    if (needsHeader) {
      const row = atoms[index].row;
      out.push({
        type: "group",
        testCode: row.testCode,
        title: row.testTitle,
        methodCode: row.methodCode,
        methodName: row.methodName,
        accredited: row.accredited,
        continued: true
      });
      used += 1;
    }
    while (index < atoms.length && used < room) {
      const atom = atoms[index];
      if (atom.type === "group" && used + 1 === room && index + 1 < atoms.length) break;
      out.push(cloneAtom(atom));
      used += 1;
      index += 1;
    }
    return { atoms: out, next: index, used };
  }

  function cloneAtom(atom) {
    if (atom.type === "group") return Object.assign({}, atom);
    return { type: "data", row: atom.row, dataIndex: atom.dataIndex, resultNotes: [] };
  }

  function paginate(samples, rows, config) {
    const atoms = buildAtoms(rows, config);
    if (config.mode === "matrix") return paginateMatrix(samples, atoms, config);
    return paginateVertical(samples, atoms, config);
  }

  function paginateVertical(samples, atoms, config) {
    const pages = [];
    let sampleIndex = 0;
    let atomIndex = 0;
    let guard = 0;
    while (sampleIndex < samples.length && guard++ < 200) {
      const blocks = [];
      let used = 0;
      let started = 0;
      let inner = 0;
      while (sampleIndex < samples.length && inner++ < 50) {
        const startingNew = atomIndex === 0;
        if (startingNew && started >= config.samplesPerPage && blocks.length) break;
        const room = config.rowsPerPage - used;
        if (room <= 0) break;
        const slice = takeAtoms(atoms, atomIndex, room, config);
        if (!slice.atoms.length) {
          if (!blocks.length) {
            blocks.push({
              mode: "vertical",
              sample: samples[sampleIndex],
              atoms: [cloneAtom(atoms[atomIndex])],
              continued: atomIndex > 0
            });
            atomIndex += 1;
          }
          break;
        }
        blocks.push({
          mode: "vertical",
          sample: samples[sampleIndex],
          atoms: slice.atoms,
          continued: atomIndex > 0
        });
        used += slice.used;
        const previous = atomIndex;
        atomIndex = slice.next;
        if (startingNew) started += 1;
        if (atomIndex <= previous) break;
        if (atomIndex >= atoms.length) {
          sampleIndex += 1;
          atomIndex = 0;
          if (used >= config.rowsPerPage) break;
        } else {
          break;
        }
      }
      if (!blocks.length) break;
      pages.push({ mode: "vertical", blocks });
    }
    return { pages, atoms };
  }

  function paginateMatrix(samples, atoms, config) {
    const groups = chunk(samples, Math.max(1, config.samplesPerPage));
    const pages = [];
    groups.forEach((group, groupIndex) => {
      let atomIndex = 0;
      let guard = 0;
      while (atomIndex < atoms.length && guard++ < 100) {
        const slice = takeAtoms(atoms, atomIndex, config.rowsPerPage, config);
        if (slice.next <= atomIndex) break;
        pages.push({
          mode: "matrix",
          blocks: [{
            mode: "matrix",
            samples: group,
            atoms: slice.atoms,
            continued: atomIndex > 0,
            sampleGroupIndex: groupIndex,
            sampleGroupCount: groups.length
          }]
        });
        atomIndex = slice.next;
      }
    });
    return { pages, atoms };
  }

  function chunk(items, size) {
    const groups = [];
    const width = Math.max(1, size);
    for (let index = 0; index < items.length; index += width) groups.push(items.slice(index, index + width));
    return groups;
  }

  function dataAtoms(atoms) {
    return (atoms || []).filter((atom) => atom.type === "data");
  }

  function finishes(atoms, rows) {
    const found = dataAtoms(atoms);
    if (!found.length || !rows.length) return false;
    return found[found.length - 1].dataIndex === rows.length - 1;
  }

  function scopeOn(config, domain, scope) {
    if (!config.domains[domain]) return false;
    const list = domain === "comment" ? config.commentScopes : config.conclusionScopes;
    return list.indexOf(scope) !== -1;
  }

  function attach(pages, rows, config) {
    const snap = snapshot();
    const lastByTest = {};
    rows.forEach((row, index) => {
      lastByTest[row.testCode] = index;
    });
    const blocked = blocking(validate(config));
    const methodOrder = [];
    pages.forEach((page) => {
      page.blocks.forEach((block) => {
        dataAtoms(block.atoms).forEach((atom) => {
          if (config.domains.accreditation && atom.row.accredited === false && methodOrder.indexOf(atom.row.methodCode) === -1) {
            methodOrder.push(atom.row.methodCode);
          }
        });
      });
    });

    pages.forEach((page, index) => {
      page.number = index + 1;
      page.total = pages.length;
      page.isFirst = index === 0;
      page.isLast = index === pages.length - 1;
      page.placed = {
        resultNotes: [],
        testNotes: [],
        sampleNotes: [],
        emptySampleComments: [],
        sampleConclusions: [],
        reportComments: [],
        reportConclusions: [],
        footnotes: []
      };
      const samplesOnPage = [];
      page.blocks.forEach((block) => {
        const blockSamples = block.mode === "matrix" ? block.samples : [block.sample];
        blockSamples.forEach((sample) => {
          if (!samplesOnPage.some((item) => item.id === sample.id)) samplesOnPage.push(sample);
        });
        const visibleTests = new Set();
        dataAtoms(block.atoms).forEach((atom) => {
          if (atom.dataIndex === lastByTest[atom.row.testCode]) visibleTests.add(atom.row.testCode);
          if (!scopeOn(config, "comment", "result")) return;
          blockSamples.forEach((sample) => {
            snap.comments.filter((comment) => comment.scope === "result" && comment.sampleId === sample.id && comment.parameterCode === atom.row.code).forEach((comment) => {
              atom.resultNotes.push({ sampleId: sample.id, parameterCode: atom.row.code, body: comment.body });
              page.placed.resultNotes.push({ sampleId: sample.id, parameterCode: atom.row.code, body: comment.body });
            });
          });
        });
        if (scopeOn(config, "comment", "test")) {
          visibleTests.forEach((testCode) => {
            blockSamples.forEach((sample) => {
              snap.comments.filter((comment) => comment.scope === "test" && comment.testCode === testCode && comment.sampleId === sample.id).forEach((comment) => {
                page.placed.testNotes.push({ sampleId: sample.id, testCode, body: comment.body });
              });
            });
          });
        }
        const sampleFinishes = block.mode === "matrix" ? finishes(block.atoms, rows) : finishes(block.atoms, rows);
        if (sampleFinishes && scopeOn(config, "comment", "sample")) {
          blockSamples.forEach((sample) => {
            const notes = snap.comments.filter((comment) => comment.scope === "sample" && comment.sampleId === sample.id);
            if (notes.length) {
              notes.forEach((comment) => page.placed.sampleNotes.push({ sampleId: sample.id, body: comment.body }));
            } else {
              page.placed.emptySampleComments.push(sample.id);
            }
          });
        }
        if (sampleFinishes && scopeOn(config, "conclusion", "sample")) {
          blockSamples.forEach((sample) => {
            snap.conclusions.filter((item) => item.scope === "sample" && item.sampleId === sample.id).forEach((item) => {
              page.placed.sampleConclusions.push({ sampleId: sample.id, body: item.body });
            });
          });
        }
      });
      page.sampleIds = samplesOnPage.map((sample) => sample.id);
      const indexes = page.blocks.flatMap((block) => dataAtoms(block.atoms).map((atom) => atom.dataIndex));
      page.rowFrom = indexes.length ? Math.min.apply(null, indexes) + 1 : 0;
      page.rowTo = indexes.length ? Math.max.apply(null, indexes) + 1 : 0;
      page.rowContinued = page.blocks.some((block) => block.continued);
      page.signoff = page.isLast && config.signoff && config.domains.report && !blocked;
      page.signoffWithheld = page.isLast && config.signoff && blocked;
      if (page.isFirst && scopeOn(config, "comment", "report")) {
        page.placed.reportComments = snap.comments.filter((comment) => comment.scope === "report").map((comment) => comment.body);
      }
      if (page.isLast && scopeOn(config, "conclusion", "report")) {
        page.placed.reportConclusions = snap.conclusions.filter((item) => item.scope === "report").map((item) => item.body);
      }
      const seen = new Set();
      page.blocks.forEach((block) => {
        dataAtoms(block.atoms).forEach((atom) => {
          if (!config.domains.accreditation || atom.row.accredited !== false) return;
          if (seen.has(atom.row.methodCode)) return;
          seen.add(atom.row.methodCode);
          page.placed.footnotes.push({
            n: methodOrder.indexOf(atom.row.methodCode) + 1,
            methodCode: atom.row.methodCode,
            text: `${atom.row.methodCode} (${atom.row.methodName}) is outside the fictional accreditation scope. The mark is a layout flag, not a certificate.`
          });
        });
      });
      page.banner = bannerFor(page);
    });
    return pages;
  }

  function bannerFor(page) {
    const sim = `Simulated page ${page.number} of ${page.total} · not a PDF`;
    const span = page.sampleIds.length > 1 ? `${page.sampleIds[0]} to ${page.sampleIds[page.sampleIds.length - 1]}` : (page.sampleIds[0] || "");
    const bits = [];
    if (page.rowContinued) bits.push("Continued");
    const matrixBlock = page.blocks.find((block) => block.mode === "matrix");
    if (matrixBlock) bits.push(`sample group ${matrixBlock.sampleGroupIndex + 1} of ${matrixBlock.sampleGroupCount}`);
    if (span) bits.push(span);
    if (page.rowFrom) bits.push(`rows ${page.rowFrom}–${page.rowTo}`);
    if (page.number > 1) bits.push("headers repeated");
    return { sim, detail: bits.join(" · ") };
  }

  function effectiveColumns(config) {
    return COLUMN_KEYS.filter((key) => {
      if (!config.columns[key]) return false;
      if ((key === "result" || key === "mu") && !config.domains.result) return false;
      if ((key === "limit" || key === "evaluation") && !config.domains.mrl) return false;
      if (key === "accreditation" && !config.domains.accreditation) return false;
      if (key === "method" && !config.domains.test) return false;
      if ((key === "unit" || key === "loq" || key === "lor" || key === "reference") && !config.domains.parameter) return false;
      return true;
    });
  }

  function sharedColumns(config) {
    return effectiveColumns(config).filter((key) => key !== "result" && key !== "mu" && key !== "evaluation");
  }

  function matchedLimit(config, row) {
    if (!config.domains.mrl) return null;
    if (!config.mrl.requireUnit || !config.mrl.requireMatrix || !config.mrl.requireJurisdiction) return null;
    const candidates = snapshot().limits.filter((item) => item.parameterCode === row.code);
    const full = candidates.filter((item) => {
      if (item.jurisdiction !== config.mrl.jurisdiction) return false;
      if (item.matrix !== config.matrix) return false;
      if (item.unit !== row.unit) return false;
      return true;
    });
    return full[0] || null;
  }

  function evaluateCell(config, row, sampleId) {
    const result = row.results[sampleId] || { status: "missing" };
    if (!config.domains.mrl) return { kind: "off", label: "" };
    if (!config.mrl.requireUnit || !config.mrl.requireMatrix || !config.mrl.requireJurisdiction) {
      return { kind: "blocked", label: "Blocked — match rules are incomplete" };
    }
    if (!config.domains.result || result.status === "missing") {
      return { kind: "missing-result", label: "Not evaluated — result not reported" };
    }
    const candidates = snapshot().limits.filter((item) => item.parameterCode === row.code);
    if (!candidates.length) return { kind: "none", label: "No applicable fictional limit", limit: null };
    const full = candidates.filter((item) => {
      if (config.mrl.requireJurisdiction && item.jurisdiction !== config.mrl.jurisdiction) return false;
      if (config.mrl.requireMatrix && item.matrix !== config.matrix) return false;
      if (config.mrl.requireUnit && item.unit !== row.unit) return false;
      return true;
    });
    if (!full.length) {
      const reasons = mismatchReasons(candidates, config, row.unit);
      return { kind: "mismatch", reasons, label: mismatchLabel(reasons), limit: null };
    }
    const match = full[0];
    if (result.status === "notDetected") {
      return { kind: "nd", limit: match, label: "Not evaluated — below the reporting limit, not a pass or fail" };
    }
    if (result.status === "numeric") {
      if (result.value <= match.limit) return { kind: "within", limit: match, label: "Within fictional limit" };
      return { kind: "exceeds", limit: match, label: "Exceeds fictional limit" };
    }
    return { kind: "other", label: "Not evaluated", limit: null };
  }

  function mismatchReasons(candidates, config, unit) {
    const dims = [];
    if (config.mrl.requireUnit) dims.push("unit");
    if (config.mrl.requireMatrix) dims.push("matrix");
    if (config.mrl.requireJurisdiction) dims.push("jurisdiction");
    const flagsFor = (dropped) => ({
      unit: config.mrl.requireUnit && dropped !== "unit",
      matrix: config.mrl.requireMatrix && dropped !== "matrix",
      jurisdiction: config.mrl.requireJurisdiction && dropped !== "jurisdiction"
    });
    const matches = (item, flags) => {
      if (flags.jurisdiction && item.jurisdiction !== config.mrl.jurisdiction) return false;
      if (flags.matrix && item.matrix !== config.matrix) return false;
      if (flags.unit && item.unit !== unit) return false;
      return true;
    };
    const blockers = dims.filter((dim) => candidates.some((item) => matches(item, flagsFor(dim))));
    if (blockers.length) return blockers;
    return dims.filter((dim) => candidates.every((item) => {
      if (dim === "unit") return item.unit !== unit;
      if (dim === "matrix") return item.matrix !== config.matrix;
      return item.jurisdiction !== config.mrl.jurisdiction;
    }));
  }

  function mismatchLabel(reasons) {
    if (!reasons.length) return "Not evaluated — no full match";
    if (reasons.length === 1) return `Not evaluated — ${reasons[0]} does not match the fictional limit`;
    return `Not evaluated — ${reasons.join(" and ")} do not match the fictional limit`;
  }

  function handlersFor(config) {
    const columnsOn = effectiveColumns(config);
    return {
      report: config.domains.report ? "first-page-block-plus-running-mock-header" : "running-mock-header-only",
      sample: config.mode === "matrix" ? "shared-parameter-rows-samples-as-columns" : "stacked-sample-blocks",
      test: config.groupParameters && config.domains.test ? "group-header-row" : "flat-parameter-list",
      parameter: "row-identity",
      result: config.mode === "matrix" ? "per-sample-cell" : "result-column",
      measurementUncertainty: columnsOn.indexOf("mu") === -1
        ? "hidden"
        : (config.mode === "matrix" ? "inline-in-result-cell" : "mu-column"),
      comment: "scoped-note-on-owning-page",
      conclusion: "end-matter-or-sample-close",
      mrl: config.domains.mrl ? "match-unit-matrix-jurisdiction-else-not-evaluated" : "off",
      accreditation: config.domains.accreditation ? "footnote-on-pages-that-show-the-method" : "off",
      signoff: config.signoff ? "last-simulated-page-only" : "off",
      pagination: "simulated-horizontal-groups-and-vertical-row-chunks"
    };
  }

  function statsFor(samples, rows) {
    let missing = 0;
    let notDetected = 0;
    let numeric = 0;
    samples.forEach((sample) => {
      rows.forEach((row) => {
        const cell = row.results[sample.id];
        if (!cell || cell.status === "missing") missing += 1;
        else if (cell.status === "notDetected") notDetected += 1;
        else if (cell.status === "numeric") numeric += 1;
      });
    });
    const tests = [];
    rows.forEach((row) => {
      if (!tests.some((item) => item.testCode === row.testCode)) {
        tests.push({ testCode: row.testCode, accredited: row.accredited });
      }
    });
    return {
      samples: samples.length,
      tests: tests.length,
      unaccreditedTests: tests.filter((item) => !item.accredited).length,
      parameters: rows.length,
      slots: samples.length * rows.length,
      missing,
      notDetected,
      numeric
    };
  }

  function build(input) {
    const config = normalize(input);
    const issues = validate(config);
    const handlers = handlersFor(config);
    const selectionSamples = snapSamplesSafe(config);
    const rows = snapshot().matrices[config.matrix] ? flattenRows(config) : [];
    const withheld = identityBlocked(issues)
      ? issues.filter((item) => item.level === "error").map((item) => item.message).join(" ")
      : "";
    let pages = [];
    if (!withheld && selectionSamples.length && rows.length) {
      const paginated = paginate(selectionSamples, rows, config);
      pages = attach(paginated.pages, rows, config);
    }
    const stats = statsFor(selectionSamples, rows);
    stats.available = availableCount(config.matrix);
    return {
      config,
      issues,
      withheld,
      samples: selectionSamples,
      rows,
      pages,
      stats,
      handlers,
      effectiveColumns: effectiveColumns(config),
      sharedColumns: sharedColumns(config),
      selection: {
        matrix: config.matrix,
        sampleIds: selectionSamples.map((sample) => sample.id),
        parameterCodes: rows.map((row) => row.code),
        testCodes: rows.reduce((list, row) => {
          if (list.indexOf(row.testCode) === -1) list.push(row.testCode);
          return list;
        }, [])
      }
    };
  }

  function snapSamplesSafe(config) {
    if (!snapshot().matrices[config.matrix]) return [];
    if (!Number.isInteger(config.sampleCount) || config.sampleCount < 1) return [];
    return selectSamples(config);
  }

  function exportDocument(model) {
    const built = model && model.pages ? model : build(model);
    return {
      schemaVersion: SCHEMA,
      documentKind: "nordic-layout-config",
      banner: "MOCK / NOT FOR RELEASE",
      feeds: FEEDS,
      snapshotRef: {
        id: snapshot().id,
        feed: snapshot().feed,
        reportNumber: snapshot().report.reportNumber,
        note: "Presets do not embed report HTML. The renderer reads this snapshot and the layout document."
      },
      presetId: built.config.presetId,
      layout: clone(built.config),
      selection: built.selection,
      handlers: built.handlers,
      pagination: {
        simulated: true,
        pdf: false,
        gap: "Simulated groups and row chunks with repeated headers. No print engine and no PDF in this sandbox.",
        pageCount: built.pages.length,
        withheld: built.withheld || "",
        samplesPerPage: built.config.samplesPerPage,
        rowsPerPage: built.config.rowsPerPage,
        mode: built.config.mode,
        pages: built.pages.map((page) => ({
          number: page.number,
          sampleIds: page.sampleIds,
          rowFrom: page.rowFrom,
          rowTo: page.rowTo,
          continued: page.rowContinued,
          signoff: page.signoff,
          footnotes: page.placed.footnotes.map((note) => note.methodCode),
          resultNotes: page.placed.resultNotes.map((note) => note.parameterCode + "@" + note.sampleId),
          sampleNotes: page.placed.sampleNotes.map((note) => note.sampleId)
        }))
      },
      validation: built.issues
    };
  }

  function isEdited(config) {
    const current = normalize(config);
    const fresh = configFromPreset(current.presetId);
    return JSON.stringify(current) !== JSON.stringify(fresh);
  }

  window.NordicLogic = {
    SCHEMA,
    STORAGE_KEY,
    FEEDS,
    PRESETS,
    COLUMN_KEYS,
    COLUMN_LABELS,
    DOMAIN_KEYS,
    DOMAIN_LABELS,
    SCOPE_KEYS,
    preset,
    configFromPreset,
    normalize,
    availableCount,
    validate,
    build,
    exportDocument,
    evaluateCell,
    matchedLimit,
    isEdited,
    effectiveColumns
  };
})();
