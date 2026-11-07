/* Nordic examples sandbox. Storage is separate from the layout studio. */
(function () {
  const LOGIC = () => window.NordicLogic;
  const RENDER = () => window.NordicRender;
  const state = { config: null, view: "preview" };
  let wired = false;

  function load() {
    try {
      const raw = localStorage.getItem(LOGIC().STORAGE_KEY);
      if (!raw) return LOGIC().configFromPreset("harbour-vertical");
      const parsed = JSON.parse(raw);
      if (!parsed || parsed.schemaVersion !== LOGIC().SCHEMA || !parsed.config) throw new Error("schema");
      return LOGIC().normalize(parsed.config);
    } catch (error) {
      return LOGIC().configFromPreset("harbour-vertical");
    }
  }

  function save() {
    localStorage.setItem(LOGIC().STORAGE_KEY, JSON.stringify({
      schemaVersion: LOGIC().SCHEMA,
      config: state.config
    }));
  }

  function show() {
    document.body.classList.add("mode-nordic");
    document.getElementById("nordicApp").hidden = false;
    if (!state.config) state.config = load();
    ensureControls();
    syncControls();
    render();
  }

  function hide() {
    document.body.classList.remove("mode-nordic");
    const root = document.getElementById("nordicApp");
    if (root) root.hidden = true;
  }

  function ensureControls() {
    if (wired) return;
    wired = true;
    const root = document.getElementById("nordicApp");
    document.getElementById("nordicPresets").innerHTML = LOGIC().PRESETS.map((item) =>
      `<button type="button" class="preset" data-preset="${item.id}"><strong>${item.name}</strong><span>${item.blurb}</span></button>`
    ).join("");
    document.getElementById("nordicDomains").innerHTML = LOGIC().DOMAIN_KEYS.map((key) =>
      `<label class="check"><input type="checkbox" data-domain="${key}" /> ${LOGIC().DOMAIN_LABELS[key]}</label>`
    ).join("");
    document.getElementById("nordicColumns").innerHTML = LOGIC().COLUMN_KEYS.map((key) =>
      `<label class="check"><input type="checkbox" data-column="${key}" /> ${LOGIC().COLUMN_LABELS[key]}</label>`
    ).join("");
    const scopeBoxes = (name) => LOGIC().SCOPE_KEYS.map((scope) =>
      `<label class="check"><input type="checkbox" data-${name}-scope="${scope}" /> ${scope}</label>`
    ).join("");
    document.getElementById("nordicCommentScopes").innerHTML = scopeBoxes("comment");
    document.getElementById("nordicConclusionScopes").innerHTML = scopeBoxes("conclusion");
    root.addEventListener("click", onClick);
    root.addEventListener("change", onChange);
    root.addEventListener("input", onChange);
  }

  function onClick(event) {
    const presetButton = event.target.closest("button[data-preset]");
    if (presetButton) {
      state.config = LOGIC().configFromPreset(presetButton.dataset.preset);
      save();
      syncControls();
      render();
      return;
    }
    if (event.target.closest("#nordicReset")) {
      localStorage.removeItem(LOGIC().STORAGE_KEY);
      state.config = LOGIC().configFromPreset("harbour-vertical");
      syncControls();
      render();
      return;
    }
    if (event.target.closest("#nordicExport")) {
      const model = LOGIC().build(state.config);
      const blob = new Blob([JSON.stringify(LOGIC().exportDocument(model), null, 2)], { type: "application/json" });
      const url = URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = url;
      link.download = "northline-nordic-layout.json";
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(url);
      return;
    }
    const view = event.target.closest("[data-nordic-view]");
    if (view) {
      state.view = view.dataset.nordicView;
      render();
    }
  }

  function onChange(event) {
    const target = event.target;
    if (target.dataset.domain) {
      state.config.domains[target.dataset.domain] = target.checked;
      if (target.dataset.domain === "mrl") {
        state.config.columns.limit = target.checked;
        state.config.columns.evaluation = target.checked;
      }
      if (target.dataset.domain === "accreditation") state.config.columns.accreditation = target.checked;
      if (target.dataset.domain === "test" && !target.checked) state.config.groupParameters = false;
      commitFromControl();
      return;
    }
    if (target.dataset.column) {
      state.config.columns[target.dataset.column] = target.checked;
      commitFromControl();
      return;
    }
    if (target.dataset.commentScope) {
      toggleScope("commentScopes", target.dataset.commentScope, target.checked);
      return;
    }
    if (target.dataset.conclusionScope) {
      toggleScope("conclusionScopes", target.dataset.conclusionScope, target.checked);
      return;
    }
    if (target.id === "nordicMode") state.config.mode = target.value;
    else if (target.id === "nordicMatrix") {
      state.config.matrix = target.value;
      const available = LOGIC().availableCount(state.config.matrix);
      if (state.config.sampleCount > available) state.config.sampleCount = available;
    } else if (target.id === "nordicSampleCount") state.config.sampleCount = Number(target.value);
    else if (target.id === "nordicSamplesPerPage") state.config.samplesPerPage = Number(target.value);
    else if (target.id === "nordicRowsPerPage") state.config.rowsPerPage = Number(target.value);
    else if (target.id === "nordicGroup") state.config.groupParameters = target.checked;
    else if (target.id === "nordicJurisdiction") state.config.mrl.jurisdiction = target.value;
    else if (target.id === "nordicReqUnit") state.config.mrl.requireUnit = target.checked;
    else if (target.id === "nordicReqMatrix") state.config.mrl.requireMatrix = target.checked;
    else if (target.id === "nordicReqJurisdiction") state.config.mrl.requireJurisdiction = target.checked;
    else if (target.id === "nordicSignoff") state.config.signoff = target.checked;
    else return;
    commitFromControl();
  }

  function toggleScope(key, scope, checked) {
    const set = new Set(state.config[key]);
    if (checked) set.add(scope);
    else set.delete(scope);
    state.config[key] = LOGIC().SCOPE_KEYS.filter((item) => set.has(item));
    commitFromControl();
  }

  function commitFromControl() {
    save();
    syncControls();
    render();
  }

  function syncControls() {
    const config = state.config;
    setValue("nordicMode", config.mode);
    setValue("nordicMatrix", config.matrix);
    const count = document.getElementById("nordicSampleCount");
    count.max = String(LOGIC().availableCount(config.matrix));
    setValue("nordicSampleCount", config.sampleCount);
    setValue("nordicSamplesPerPage", config.samplesPerPage);
    setValue("nordicRowsPerPage", config.rowsPerPage);
    setChecked("nordicGroup", config.groupParameters);
    setChecked("nordicReqUnit", config.mrl.requireUnit);
    setChecked("nordicReqMatrix", config.mrl.requireMatrix);
    setChecked("nordicReqJurisdiction", config.mrl.requireJurisdiction);
    setChecked("nordicSignoff", config.signoff);
    setValue("nordicJurisdiction", config.mrl.jurisdiction);
    document.querySelectorAll("[data-domain]").forEach((input) => {
      input.checked = Boolean(config.domains[input.dataset.domain]);
    });
    document.querySelectorAll("[data-column]").forEach((input) => {
      input.checked = Boolean(config.columns[input.dataset.column]);
    });
    document.querySelectorAll("[data-comment-scope]").forEach((input) => {
      input.checked = config.commentScopes.indexOf(input.dataset.commentScope) !== -1;
    });
    document.querySelectorAll("[data-conclusion-scope]").forEach((input) => {
      input.checked = config.conclusionScopes.indexOf(input.dataset.conclusionScope) !== -1;
    });
    document.querySelectorAll("[data-preset]").forEach((button) => {
      button.classList.toggle("is-on", button.dataset.preset === config.presetId);
    });
    const edited = document.getElementById("nordicEdited");
    const dirty = LOGIC().isEdited(config);
    edited.hidden = !dirty;
    const app = document.getElementById("nordicApp");
    app.dataset.activePreset = config.presetId;
    app.dataset.mode = config.mode;
    app.dataset.edited = dirty ? "true" : "false";
  }

  function setValue(id, value) {
    const el = document.getElementById(id);
    if (!el) return;
    const next = String(value);
    if (el.value !== next) el.value = next;
  }

  function setChecked(id, value) {
    const el = document.getElementById(id);
    if (!el) return;
    el.checked = Boolean(value);
  }

  function render() {
    const model = LOGIC().build(state.config);
    document.getElementById("nordicPreview").hidden = state.view !== "preview";
    document.getElementById("nordicJsonView").hidden = state.view !== "json";
    document.getElementById("nordicRefs").hidden = state.view !== "refs";
    document.querySelectorAll("[data-nordic-view]").forEach((button) => {
      button.classList.toggle("is-on", button.dataset.nordicView === state.view);
    });
    if (state.view === "preview") {
      document.getElementById("nordicPreview").innerHTML = RENDER().renderPreview(model);
    }
    document.getElementById("nordicJson").textContent = JSON.stringify(LOGIC().exportDocument(model), null, 2);
    document.getElementById("nordicMap").innerHTML = RENDER().renderMap(model);
    const errors = model.issues.filter((item) => item.level === "error");
    const warnings = model.issues.filter((item) => item.level === "warning");
    const summary = document.getElementById("nordicValidationSummary");
    if (errors.length) {
      summary.textContent = `${errors.length} blocking`;
      summary.className = "bad";
    } else if (warnings.length) {
      summary.textContent = `${warnings.length} warning${warnings.length === 1 ? "" : "s"}`;
      summary.className = "warn";
    } else {
      summary.textContent = "No blocking issues";
      summary.className = "ok";
    }
    const list = document.getElementById("nordicValidation");
    list.innerHTML = model.issues.length
      ? model.issues.map((item) => `<li class="${item.level}" data-code="${item.code}"><strong>${item.code}</strong> ${escapeText(item.message)}</li>`).join("")
      : `<li class="ok">This layout can be rendered from the shared snapshot.</li>`;
    document.getElementById("nordicPageCount").textContent = model.withheld
      ? "Preview withheld"
      : `${model.pages.length} simulated page${model.pages.length === 1 ? "" : "s"}`;
  }

  function escapeText(value) {
    return String(value)
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;");
  }

  window.NorthlineNordic = {
    show,
    hide,
    load,
    getConfig: () => state.config,
    build: () => LOGIC().build(state.config)
  };

  if (location.hash === "#nordic" && location.search.indexOf("selfcheck=1") === -1) {
    const tab = document.querySelector('.tabs [data-tab="nordic"]');
    if (tab) tab.click();
  }
})();
