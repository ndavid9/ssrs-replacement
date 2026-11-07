/* Interaction checks for the Nordic sandbox and the existing layout studio. */
(function () {
  if (location.search.indexOf("selfcheck=1") === -1) return;

  const failures = [];
  function assert(condition, message) {
    if (!condition) failures.push(message);
  }

  function pages() {
    return Array.from(document.querySelectorAll(".sim-page"));
  }

  function text() {
    return document.getElementById("nordicPreview").textContent || "";
  }

  function clickPreset(id) {
    const button = document.querySelector(`[data-preset="${id}"]`);
    if (!button) throw new Error("missing preset " + id);
    button.click();
  }

  function ensureNordic() {
    if (!document.body.classList.contains("mode-nordic")) {
      document.querySelector('.tabs [data-tab="nordic"]').click();
    }
  }

  function setNumber(id, value) {
    const el = document.getElementById(id);
    el.focus();
    el.value = String(value);
    el.dispatchEvent(new Event("input", { bubbles: true }));
    el.dispatchEvent(new Event("change", { bubbles: true }));
  }

  function setChecked(selector, checked) {
    const el = document.querySelector(selector);
    el.checked = checked;
    el.dispatchEvent(new Event("change", { bubbles: true }));
  }

  function phaseBag() {
    return JSON.parse(sessionStorage.getItem("nordic-selfcheck") || "{}");
  }

  function saveBag(patch) {
    sessionStorage.setItem("nordic-selfcheck", JSON.stringify(Object.assign(phaseBag(), patch)));
  }

  function finish(ok) {
    const bag = phaseBag();
    window.__nordicSelfcheck = {
      done: true,
      ok: ok && failures.length === 0 && !(bag.failures || []).length,
      failures: (bag.failures || []).concat(failures),
      pageErrors: window.__pageErrors || [],
      notes: bag.notes || []
    };
    sessionStorage.removeItem("nordic-selfcheck");
  }

  function rememberFailure() {
    if (!failures.length) return;
    const bag = phaseBag();
    saveBag({ failures: (bag.failures || []).concat(failures) });
    failures.length = 0;
  }

  try {
    const phase = phaseBag().phase || "studio";
    if (phase === "studio") phaseStudio();
    else if (phase === "persist") phasePersist();
    else phaseAfterReset();
  } catch (error) {
    failures.push(String(error && error.stack || error));
    finish(false);
  }

  function phaseStudio() {
    assert(document.getElementById("panel-schema") && !document.getElementById("panel-schema").hidden, "schema panel starts visible");
    assert(document.getElementById("nordicApp").hidden, "nordic sandbox starts hidden");
    document.querySelector('.tabs [data-tab="layout"]').click();
    assert(document.querySelectorAll("#sectionList .card").length > 0, "layout editor still lists sections");
    const preview = document.getElementById("previewDoc").textContent;
    assert(preview.indexOf("SYNTHETIC DATA") !== -1, "studio preview banner missing");
    assert(preview.indexOf("Food Export") !== -1 || preview.indexOf("Synthetic residue screen") !== -1, "studio preview content missing");
    const name = document.getElementById("typeName");
    name.dispatchEvent(new Event("input", { bubbles: true }));
    const studioKey = localStorage.getItem("northline-layout-studio-v1");
    assert(Boolean(studioKey), "studio storage was not written");
    ensureNordic();
    assert(document.body.classList.contains("mode-nordic"), "nordic mode class missing");
    assert(document.getElementById("nordicApp").hidden === false, "nordic panel hidden after open");
    assert(document.querySelector(".shell").offsetParent === null, "studio shell still visible in nordic mode");

    ["harbour-vertical", "basin-groups", "plot-matrix", "material-footnotes", "flush-limits"].forEach((id) => {
      clickPreset(id);
      assert(document.getElementById("nordicApp").dataset.activePreset === id, "preset did not activate " + id);
      assert(pages().length >= 1, "preset rendered no page " + id);
      assert(text().indexOf("MOCK / NOT FOR RELEASE") !== -1, "mock banner missing " + id);
      assert(text().indexOf("Simulated page") !== -1, "simulated pagination label missing " + id);
      assert(text().indexOf("not a PDF") !== -1, "pdf disclaimer missing " + id);
    });

    clickPreset("harbour-vertical");
    const harbour = pages();
    assert(harbour.length >= 2, "harbour should continue onto another simulated page");
    assert(harbour[0].dataset.signoff === "false", "sign-off appeared before the last harbour page");
    assert(harbour[harbour.length - 1].dataset.signoff === "true", "sign-off missing on the last harbour page");
    assert(harbour[0].textContent.indexOf("Printed on the first simulated page only") !== -1, "report comment missing on first page");
    assert(harbour[0].textContent.indexOf("Printed once, on the last simulated page") === -1, "report conclusion leaked onto the first page");
    assert(harbour[harbour.length - 1].textContent.indexOf("Printed once, on the last simulated page") !== -1, "report conclusion missing on the last page");
    assert((text().match(/Result note bound to FX-WS1 on MOCK-PW-01/g) || []).length === 1, "result note was not tied to one row");
    const harbourFoot = harbour.filter((page) => page.textContent.indexOf("W-MOCK-11") !== -1);
    assert(harbourFoot.length >= 1, "unaccredited harbour method not visible");
    harbour.forEach((page) => {
      const shows = page.textContent.indexOf("W-MOCK-11") !== -1;
      const noted = page.textContent.indexOf("outside the fictional accreditation scope") !== -1;
      assert(shows === noted, "harbour footnote was not tied to the page that shows W-MOCK-11");
    });

    clickPreset("material-footnotes");
    pages().forEach((page) => {
      const shows = page.textContent.indexOf("H-MOCK-09") !== -1;
      const noted = page.querySelector('[data-footnote="H-MOCK-09"]');
      assert(Boolean(shows) === Boolean(noted), "material footnote was not tied to H-MOCK-09");
    });
    assert(text().indexOf("Not accredited") !== -1, "not-accredited marker missing");
    setChecked('[data-domain="accreditation"]', false);
    assert(text().indexOf("outside the fictional accreditation scope") === -1, "footnote remained after accreditation was turned off");
    assert(text().indexOf("not reported") !== -1 || text().indexOf("2.2") !== -1, "material results disappeared with accreditation");

    clickPreset("plot-matrix");
    const plot = pages();
    assert(plot.length >= 4, "plot matrix did not paginate in both directions");
    assert(plot[0].dataset.samples.indexOf("MOCK-S-01") !== -1, "first matrix page missing MOCK-S-01");
    assert(plot[0].dataset.samples.indexOf("MOCK-S-04") !== -1, "first matrix page missing MOCK-S-04");
    assert(plot[0].dataset.samples.indexOf("MOCK-S-05") === -1, "first matrix page included the second sample group");
    assert(plot.some((page) => page.dataset.samples.indexOf("MOCK-S-05") !== -1), "second sample group missing");
    plot.forEach((page) => assert(page.querySelector("thead"), "matrix page missing a repeated header"));
    const resultPages = plot.filter((page) => page.textContent.indexOf("Result note bound to FX-SM2 on MOCK-S-03") !== -1);
    assert(resultPages.length === 1, "matrix result note should appear once");
    assert(resultPages[0].dataset.samples.indexOf("MOCK-S-03") !== -1, "result note was not on the sample that owns it");
    assert(plot[0].textContent.indexOf("Sample note bound to MOCK-S-02") === -1, "sample note appeared before that sample's rows finished");
    assert(plot.some((page) => page.dataset.samples.indexOf("MOCK-S-02") !== -1 && page.textContent.indexOf("Sample note bound to MOCK-S-02") !== -1), "sample note missing");
    assert(plot.filter((page) => page.dataset.signoff === "true").length === 1, "sign-off should be on one page");
    assert(plot[plot.length - 1].dataset.signoff === "true", "sign-off was not on the last matrix page");
    assert(document.getElementById("nordicMap").textContent.indexOf("nordic-snapshot-v1") !== -1, "how this maps missed the snapshot");
    assert(document.getElementById("nordicMap").textContent.indexOf("shared-parameter-rows-samples-as-columns") !== -1, "matrix handler missing");

    setNumber("nordicSamplesPerPage", 6);
    const wide = pages();
    assert(wide[0].dataset.samples.split(",").length === 6, "samples per page did not widen the matrix");
    assert(document.querySelector('#nordicValidation [data-code="spp.stress"]'), "stress warning missing above four samples");
    const wideJson = document.getElementById("nordicJson").textContent;
    assert(wideJson.indexOf('"samplesPerPage": 6') !== -1, "exported JSON did not follow samples per page");

    clickPreset("flush-limits");
    assert(text().indexOf("Within fictional limit") !== -1, "matching limit was not scored");
    assert(text().indexOf("Not evaluated — unit does not match the fictional limit") !== -1, "unit mismatch was scored or hidden");
    assert(text().indexOf("Not evaluated — matrix does not match the fictional limit") !== -1, "matrix mismatch missing");
    assert(text().indexOf("Not evaluated — jurisdiction does not match the fictional limit") !== -1, "jurisdiction mismatch missing");
    assert(text().indexOf("Not evaluated — result not reported") !== -1, "missing result was not distinguished");
    assert(text().indexOf("Not evaluated — below the reporting limit, not a pass or fail") !== -1, "not-detected was scored as a pass or fail");
    assert(document.querySelector('[data-marker="nd"]'), "not-detected marker missing");
    assert(document.querySelector('[data-marker="missing"]'), "not-reported marker missing");
    const jurisdiction = document.getElementById("nordicJurisdiction");
    jurisdiction.value = "FICTION-OTHER";
    jurisdiction.dispatchEvent(new Event("change", { bubbles: true }));
    const ec = Array.from(document.querySelectorAll('[data-eval="FX-EC"]')).map((node) => node.textContent);
    const nix = Array.from(document.querySelectorAll('[data-eval="FX-NIX"]')).map((node) => node.textContent);
    assert(ec.length >= 1 && ec.every((line) => line.indexOf("jurisdiction") !== -1), "FX-EC did not become a jurisdiction mismatch");
    assert(nix.length >= 1 && nix.every((line) => line.indexOf("Within fictional limit") !== -1), "FX-NIX did not match FICTION-OTHER");
    setChecked("#nordicReqUnit", false);
    assert(document.querySelector('#nordicValidation [data-code="mrl.unsafe"]'), "unsafe match rules were accepted");
    assert(text().indexOf("Within fictional limit") === -1, "a pass was shown without a unit rule");
    assert(text().indexOf("Blocked — match rules are incomplete") !== -1, "blocked evaluation missing");

    clickPreset("harbour-vertical");
    const harbourJson = document.getElementById("nordicJson").textContent;
    clickPreset("plot-matrix");
    const plotJson = document.getElementById("nordicJson").textContent;
    assert(harbourJson.indexOf('"schemaVersion": "nordic-sandbox-1.0"') !== -1, "schema version missing");
    assert(harbourJson.indexOf('"id": "nordic-snapshot-v1"') !== -1, "snapshot ref missing");
    assert(plotJson.indexOf('"id": "nordic-snapshot-v1"') !== -1, "plot snapshot ref changed");
    assert(harbourJson.indexOf('"mode": "vertical"') !== -1, "harbour mode missing");
    assert(plotJson.indexOf('"mode": "matrix"') !== -1, "plot mode missing");
    assert(plotJson.indexOf('"pdf": false') !== -1, "JSON claimed a PDF");
    document.getElementById("nordicExport").click();
    assert(localStorage.getItem("northline-layout-studio-v1") === studioKey, "nordic edits changed the studio storage");

    setNumber("nordicSamplesPerPage", 3);
    saveBag({
      phase: "persist",
      studioKey,
      failures: (phaseBag().failures || []).concat(failures),
      pageErrors: (phaseBag().pageErrors || []).concat(window.__pageErrors || []),
      notes: ["phase studio ok"]
    });
    failures.length = 0;
    location.hash = "#nordic";
    location.reload();
  }

  function phasePersist() {
    ensureNordic();
    assert(document.getElementById("nordicSamplesPerPage").value === "3", "samples per page did not persist");
    assert(document.getElementById("nordicApp").dataset.activePreset === "plot-matrix", "persisted preset was lost");
    assert(localStorage.getItem("northline-layout-studio-v1") === phaseBag().studioKey, "studio storage changed across reload");
    document.querySelector('.tabs [data-tab="schema"]').click();
    assert(document.getElementById("nordicApp").hidden === true, "schema tab left the sandbox open");
    assert(document.getElementById("typeList").textContent.indexOf("Food Export") !== -1, "report types missing after sandbox use");
    document.querySelector('.tabs [data-tab="layout"]').click();
    assert(document.querySelectorAll("#sectionList .card").length > 0, "editor sections missing after reload");
    ensureNordic();
    document.getElementById("nordicReset").click();
    assert(document.getElementById("nordicApp").dataset.activePreset === "harbour-vertical", "reset did not restore harbour");
    assert(localStorage.getItem("northline-nordic-sandbox-v1") == null, "reset left the sandbox key in place");
    rememberFailure();
    saveBag({
      phase: "reset",
      pageErrors: (phaseBag().pageErrors || []).concat(window.__pageErrors || []),
      notes: (phaseBag().notes || []).concat(["phase persist ok"])
    });
    location.reload();
  }

  function phaseAfterReset() {
    ensureNordic();
    assert(document.getElementById("nordicApp").dataset.activePreset === "harbour-vertical", "harbour did not load after reset");
    assert(document.getElementById("nordicSamplesPerPage").value === "1", "harbour samples per page was not restored");
    assert(localStorage.getItem("northline-layout-studio-v1") === phaseBag().studioKey, "studio storage changed by reset");
    document.querySelector('.tabs [data-tab="layout"]').click();
    assert(document.getElementById("previewDoc").textContent.indexOf("SYNTHETIC DATA") !== -1, "studio preview broke after reset");
    const allErrors = (phaseBag().pageErrors || []).concat(window.__pageErrors || []);
    assert(allErrors.length === 0, "page errors: " + allErrors.join(" | "));
    finish(true);
  }
})();
