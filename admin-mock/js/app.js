const state = {
  doc: loadDocument(),
  selectedId: null,
  tab: "schema"
};

const els = {
  typeList: document.querySelector("#typeList"),
  newTypeName: document.querySelector("#newTypeName"),
  createType: document.querySelector("#createType"),
  samplesPerPage: document.querySelector("#samplesPerPage"),
  typeName: document.querySelector("#typeName"),
  sectionList: document.querySelector("#sectionList"),
  addDomain: document.querySelector("#addDomain"),
  addSection: document.querySelector("#addSection"),
  validationList: document.querySelector("#validationList"),
  validationSummary: document.querySelector("#validationSummary"),
  previewDoc: document.querySelector("#previewDoc"),
  jsonOut: document.querySelector("#jsonOut"),
  exportBtn: document.querySelector("#exportBtn"),
  importFile: document.querySelector("#importFile"),
  resetBtn: document.querySelector("#resetBtn"),
  diagram: document.querySelector("#diagram"),
  panels: {
    schema: document.querySelector("#panel-schema"),
    layout: document.querySelector("#panel-layout"),
    json: document.querySelector("#panel-json")
  }
};

function boot() {
  if (!state.selectedId && state.doc.reportTypes[0]) state.selectedId = state.doc.reportTypes[0].id;
  drawDiagram();
  fillAddDomain();
  bind();
  render();
}

function bind() {
  document.querySelectorAll(".tabs [data-tab]").forEach((button) => {
    button.addEventListener("click", () => {
      state.tab = button.dataset.tab;
      render();
      if (window.NorthlineNordic) {
        if (state.tab === "nordic") window.NorthlineNordic.show();
        else window.NorthlineNordic.hide();
      }
    });
  });
  els.createType.addEventListener("click", createType);
  els.newTypeName.addEventListener("keydown", (event) => {
    if (event.key === "Enter") createType();
  });
  els.typeName.addEventListener("input", () => {
    const type = selected();
    if (!type) return;
    type.name = els.typeName.value;
    commit();
  });
  els.samplesPerPage.addEventListener("change", () => {
    const type = selected();
    if (!type) return;
    type.samplesPerPage = Number(els.samplesPerPage.value);
    commit();
  });
  els.addSection.addEventListener("click", () => {
    const type = selected();
    if (!type) return;
    const domain = els.addDomain.value;
    const order = type.sections.reduce((max, section) => Math.max(max, section.order), 0) + 10;
    const patch = { enabled: true, required: false };
    if (domain === "comment" || domain === "conclusion") patch.scopes = ["sample"];
    type.sections.push(sectionTemplate(domain, order, patch));
    commit();
  });
  els.sectionList.addEventListener("click", onSectionClick);
  els.sectionList.addEventListener("change", onSectionChange);
  els.sectionList.addEventListener("dragstart", onDragStart);
  els.sectionList.addEventListener("dragover", (event) => event.preventDefault());
  els.sectionList.addEventListener("drop", onDrop);
  els.exportBtn.addEventListener("click", exportJson);
  els.importFile.addEventListener("change", importJson);
  els.resetBtn.addEventListener("click", () => {
    state.doc = seedDocument();
    state.selectedId = state.doc.reportTypes[0].id;
    commit();
  });
}

function createType() {
  const name = els.newTypeName.value.trim();
  if (!name) {
    els.newTypeName.focus();
    return;
  }
  const type = freshType(name);
  const taken = new Set(state.doc.reportTypes.map((item) => item.id));
  let id = type.id;
  let n = 2;
  while (taken.has(id)) id = type.id + "-" + n++;
  type.id = id;
  state.doc.reportTypes.push(type);
  state.selectedId = type.id;
  els.newTypeName.value = "";
  state.tab = "layout";
  commit();
}

function selected() {
  return state.doc.reportTypes.find((item) => item.id === state.selectedId) || null;
}

function commit() {
  saveDocument(state.doc);
  render();
}

function render() {
  renderTypes();
  renderTabs();
  const type = selected();
  const issues = type ? validateType(type) : [];
  renderLayout(type);
  renderValidation(issues);
  renderPreviewPane(type);
  renderJson();
  drawDiagram(type);
}

function renderTypes() {
  els.typeList.innerHTML = state.doc.reportTypes.map((type) => {
    const on = type.id === state.selectedId ? " is-on" : "";
    return `<button type="button" class="type${on}" data-select="${esc(type.id)}"><strong>${esc(type.name)}</strong><span>${esc(type.id)} · ${type.samplesPerPage}/page</span></button>`;
  }).join("");
  els.typeList.querySelectorAll("[data-select]").forEach((button) => {
    button.addEventListener("click", () => {
      state.selectedId = button.dataset.select;
      render();
    });
  });
}

function renderTabs() {
  document.querySelectorAll("[data-tab]").forEach((button) => {
    button.classList.toggle("is-on", button.dataset.tab === state.tab);
  });
  Object.entries(els.panels).forEach(([name, panel]) => {
    panel.hidden = name !== state.tab;
  });
}

function renderLayout(type) {
  if (!type) {
    els.sectionList.innerHTML = "<p>Create a report type to edit its sections.</p>";
    return;
  }
  els.typeName.value = type.name;
  els.samplesPerPage.value = type.samplesPerPage;
  const ordered = [...type.sections].sort((a, b) => a.order - b.order);
  els.sectionList.innerHTML = ordered.map((section) => sectionCard(section)).join("");
}

function sectionCard(section) {
  const domain = domainById(section.domain);
  const columns = (section.columns || []).map((column) => `
    <label class="check"><input type="checkbox" data-column="${esc(section.instanceId)}|${esc(column.key)}" ${column.visible ? "checked" : ""} /> ${esc(column.label)}</label>
  `).join("");
  const scopes = (section.domain === "comment" || section.domain === "conclusion")
    ? `<fieldset><legend>Scope</legend>${SCOPE_OPTIONS.map((scope) => `
        <label class="check"><input type="checkbox" data-scope="${esc(section.instanceId)}|${scope}" ${(section.scopes || []).includes(scope) ? "checked" : ""} /> ${scope}</label>
      `).join("")}</fieldset>`
    : "";
  const rules = `
    <fieldset>
      <legend>Display rules</legend>
      <label class="check"><input type="checkbox" data-rule="${esc(section.instanceId)}|showWhenHasData" ${section.rules.showWhenHasData ? "checked" : ""} /> Show only when this section has data</label>
      <label class="check"><input type="checkbox" data-rule="${esc(section.instanceId)}|hideIfNotAccredited" ${section.rules.hideIfNotAccredited ? "checked" : ""} /> Hide when the test is not accredited</label>
      <label class="matrix">Only for matrix <input data-matrix="${esc(section.instanceId)}" value="${esc(section.rules.matrixEquals || "")}" placeholder="oil" /></label>
    </fieldset>`;
  const mrl = section.domain === "mrl" ? `
    <fieldset>
      <legend>MRL comparison</legend>
      <label>Jurisdiction <input data-mrl="${esc(section.instanceId)}|jurisdiction" value="${esc(section.mrl.jurisdiction)}" /></label>
      <label class="check"><input type="checkbox" data-mrl="${esc(section.instanceId)}|requireUnitMatch" ${section.mrl.requireUnitMatch ? "checked" : ""} /> Compare only when units match</label>
      <label class="check"><input type="checkbox" data-mrl="${esc(section.instanceId)}|showNotEvaluated" ${section.mrl.showNotEvaluated ? "checked" : ""} /> Show not-evaluated rows</label>
    </fieldset>` : "";
  const accreditation = section.domain === "accreditation" ? `
    <fieldset>
      <legend>Accreditation marking</legend>
      <label class="check"><input type="checkbox" data-acc="${esc(section.instanceId)}|footnoteNonAccredited" ${section.accreditation.footnoteNonAccredited ? "checked" : ""} /> Footnote when a test is not accredited</label>
      <label>Mark <input data-acc="${esc(section.instanceId)}|mark" value="${esc(section.accreditation.mark)}" maxlength="3" /></label>
    </fieldset>` : "";
  return `
    <article class="card" draggable="true" data-id="${esc(section.instanceId)}">
      <header>
        <span class="grip" aria-hidden="true">↕</span>
        <div>
          <strong>${esc(section.title)}</strong>
          <span class="meta">${esc(domain.multiplicity)} · ${esc(domain.edge)}</span>
        </div>
        <label class="check"><input type="checkbox" data-flag="${esc(section.instanceId)}|enabled" ${section.enabled ? "checked" : ""} /> Included</label>
        <label class="check"><input type="checkbox" data-flag="${esc(section.instanceId)}|required" ${section.required ? "checked" : ""} /> Required</label>
      </header>
      <label class="title">Title <input data-title="${esc(section.instanceId)}" value="${esc(section.title)}" /></label>
      <div class="cols">${columns}</div>
      ${scopes}
      ${rules}
      ${mrl}
      ${accreditation}
      <p class="row-actions">
        <button type="button" data-move="up" data-id="${esc(section.instanceId)}">Move up</button>
        <button type="button" data-move="down" data-id="${esc(section.instanceId)}">Move down</button>
        <button type="button" data-remove="${esc(section.instanceId)}">Remove</button>
      </p>
    </article>`;
}

function onSectionClick(event) {
  const type = selected();
  if (!type) return;
  const remove = event.target.closest("[data-remove]");
  if (remove) {
    type.sections = type.sections.filter((section) => section.instanceId !== remove.dataset.remove);
    commit();
    return;
  }
  const move = event.target.closest("[data-move]");
  if (move) {
    moveSection(type, move.dataset.id, move.dataset.move);
    commit();
  }
}

function onSectionChange(event) {
  const type = selected();
  if (!type) return;
  const target = event.target;
  if (target.dataset.flag) {
    const [id, flag] = target.dataset.flag.split("|");
    const section = byId(type, id);
    section[flag] = target.checked;
  } else if (target.dataset.column) {
    const [id, key] = target.dataset.column.split("|");
    const section = byId(type, id);
    const column = section.columns.find((item) => item.key === key);
    column.visible = target.checked;
  } else if (target.dataset.scope) {
    const [id, scope] = target.dataset.scope.split("|");
    const section = byId(type, id);
    const set = new Set(section.scopes);
    if (target.checked) set.add(scope);
    else set.delete(scope);
    section.scopes = SCOPE_OPTIONS.filter((item) => set.has(item));
  } else if (target.dataset.rule) {
    const [id, rule] = target.dataset.rule.split("|");
    byId(type, id).rules[rule] = target.checked;
  } else if (target.dataset.mrl) {
    const [id, key] = target.dataset.mrl.split("|");
    const section = byId(type, id);
    section.mrl[key] = target.type === "checkbox" ? target.checked : target.value.trim();
  } else if (target.dataset.acc) {
    const [id, key] = target.dataset.acc.split("|");
    const section = byId(type, id);
    section.accreditation[key] = target.type === "checkbox" ? target.checked : target.value;
  } else if (target.dataset.title) {
    byId(type, target.dataset.title).title = target.value;
  } else if (target.dataset.matrix) {
    byId(type, target.dataset.matrix).rules.matrixEquals = target.value.trim();
  } else {
    return;
  }
  commit();
}

function moveSection(type, id, direction) {
  const ordered = [...type.sections].sort((a, b) => a.order - b.order);
  const index = ordered.findIndex((section) => section.instanceId === id);
  const swap = direction === "up" ? index - 1 : index + 1;
  if (index < 0 || swap < 0 || swap >= ordered.length) return;
  const order = ordered[index].order;
  ordered[index].order = ordered[swap].order;
  ordered[swap].order = order;
}

let dragId = null;
function onDragStart(event) {
  const card = event.target.closest("[data-id]");
  if (!card) return;
  dragId = card.dataset.id;
  event.dataTransfer.effectAllowed = "move";
}
function onDrop(event) {
  const type = selected();
  const card = event.target.closest("[data-id]");
  if (!type || !card || !dragId || card.dataset.id === dragId) return;
  const ordered = [...type.sections].sort((a, b) => a.order - b.order);
  const from = ordered.findIndex((section) => section.instanceId === dragId);
  const to = ordered.findIndex((section) => section.instanceId === card.dataset.id);
  if (from < 0 || to < 0) return;
  const [item] = ordered.splice(from, 1);
  ordered.splice(to, 0, item);
  ordered.forEach((section, index) => {
    section.order = (index + 1) * 10;
  });
  dragId = null;
  commit();
}

function renderValidation(issues) {
  const errors = issues.filter((issue) => issue.level === "error");
  const warnings = issues.filter((issue) => issue.level === "warning");
  els.validationSummary.textContent = errors.length
    ? `${errors.length} error${errors.length === 1 ? "" : "s"}`
    : "No blocking errors";
  els.validationSummary.className = errors.length ? "bad" : "ok";
  els.validationList.innerHTML = issues.length
    ? issues.map((issue) => `<li class="${issue.level}"><strong>${esc(issue.code)}</strong> ${esc(issue.message)}</li>`).join("")
    : `<li class="ok">This report type is consistent with the domain multiplicities.</li>`;
  if (!warnings.length && !errors.length) return;
}

function renderPreviewPane(type) {
  if (!type) {
    els.previewDoc.innerHTML = "";
    return;
  }
  els.previewDoc.innerHTML = renderPreview(type);
}

function renderJson() {
  const payload = exportPayload();
  els.jsonOut.textContent = JSON.stringify(payload, null, 2);
}

function exportPayload() {
  return {
    schemaVersion: state.doc.schemaVersion,
    feeds: state.doc.feeds,
    reportTypes: state.doc.reportTypes
  };
}

function exportJson() {
  const blob = new Blob([JSON.stringify(exportPayload(), null, 2)], { type: "application/json" });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = "northline-layout.json";
  link.click();
  URL.revokeObjectURL(url);
}

function importJson(event) {
  const file = event.target.files && event.target.files[0];
  if (!file) return;
  const reader = new FileReader();
  reader.onload = () => {
    try {
      const parsed = JSON.parse(String(reader.result));
      if (!parsed || parsed.schemaVersion !== LAYOUT_SCHEMA_VERSION || !Array.isArray(parsed.reportTypes)) {
        throw new Error("schema");
      }
      parsed.feeds = Object.assign({}, FEEDS, parsed.feeds || {});
      Object.keys(parsed.feeds).forEach((key) => {
        if (!FEEDS[key]) delete parsed.feeds[key];
      });
      state.doc = parsed;
      state.selectedId = parsed.reportTypes[0] ? parsed.reportTypes[0].id : null;
      commit();
    } catch {
      els.validationList.insertAdjacentHTML("afterbegin", `<li class="error"><strong>import</strong> That file is not a layout document ${LAYOUT_SCHEMA_VERSION}.</li>`);
    }
  };
  reader.readAsText(file);
  event.target.value = "";
}

function fillAddDomain() {
  els.addDomain.innerHTML = DOMAINS.map((domain) => `<option value="${domain.id}">${domain.label}</option>`).join("");
}

function drawDiagram(type) {
  const enabled = new Set((type ? type.sections : []).filter((section) => section.enabled).map((section) => section.domain));
  const nodes = [
    ["report", 24, 28],
    ["sample", 168, 28],
    ["test", 312, 28],
    ["parameter", 456, 28],
    ["result", 620, 28],
    ["mrl", 456, 128],
    ["accreditation", 312, 128],
    ["comment", 24, 128],
    ["conclusion", 168, 128]
  ];
  const edges = [
    [80, 52, 168, 52, "1..*"],
    [236, 52, 312, 52, "1..*"],
    [380, 52, 456, 52, "1..*"],
    [548, 52, 620, 52, "0..1"],
    [500, 78, 500, 128, "0..*"],
    [356, 78, 356, 128, "0..*"]
  ];
  const boxes = nodes.map(([id, x, y]) => {
    const domain = domainById(id);
    const on = !type || enabled.has(id) ? "on" : "off";
    return `<g class="node ${on}" transform="translate(${x} ${y})">
      <rect width="112" height="44" rx="8"></rect>
      <text x="56" y="18">${esc(domain.label)}</text>
      <text x="56" y="34" class="small">${esc(domain.multiplicity)}</text>
    </g>`;
  }).join("");
  const lines = edges.map(([x1, y1, x2, y2, label]) => {
    const mx = (x1 + x2) / 2;
    const my = (y1 + y2) / 2;
    return `<line x1="${x1}" y1="${y1}" x2="${x2}" y2="${y2}"></line><text class="edge" x="${mx}" y="${my - 4}">${label}</text>`;
  }).join("");
  els.diagram.innerHTML = `
    <svg viewBox="0 0 760 190" role="img" aria-label="Domain multiplicities">
      ${lines}
      ${boxes}
    </svg>
    <p class="diagram-note">Comments and conclusions are 0..* and may attach to report, sample, test, or result. Highlighted boxes are included on the selected report type.</p>`;
}

function byId(type, id) {
  return type.sections.find((section) => section.instanceId === id);
}

boot();
