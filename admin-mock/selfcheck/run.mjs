import fs from "node:fs";
import vm from "node:vm";
import path from "node:path";
import { spawn } from "node:child_process";
import { fileURLToPath } from "node:url";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");

function loadLogic() {
  const context = { console };
  context.window = context;
  vm.createContext(context);
  for (const file of ["nordic-snapshot.js", "nordic-logic.js"]) {
    vm.runInContext(fs.readFileSync(path.join(root, "js", file), "utf8"), context, { filename: file });
  }
  return context.NordicLogic;
}

function assert(condition, message) {
  if (!condition) throw new Error(message);
}

function unit() {
  const logic = loadLogic();
  const failures = [];
  logic.PRESETS.forEach((item) => {
    const issues = logic.validate(logic.configFromPreset(item.id));
    if (issues.length) failures.push(item.id + " " + issues.map((issue) => issue.code).join(","));
  });
  assert(failures.length === 0, "presets are not clean: " + failures.join(" | "));

  const harbour = logic.build(logic.configFromPreset("harbour-vertical"));
  assert(harbour.pages.length >= 2, "harbour pages " + harbour.pages.length);
  assert(harbour.pages[0].signoff === false, "harbour signoff on first");
  assert(harbour.pages.at(-1).signoff === true, "harbour signoff missing");
  assert(harbour.pages[0].placed.reportComments.length === 1, "harbour report comment");
  assert(harbour.pages.at(-1).placed.reportConclusions.length === 1, "harbour conclusion");
  assert(harbour.pages.slice(0, -1).every((page) => page.placed.reportConclusions.length === 0), "conclusion repeated");
  assert(harbour.pages[0].placed.resultNotes.some((note) => note.parameterCode === "FX-WS1"), "result note page");
  harbour.pages.forEach((page) => {
    const shows = page.blocks.some((block) => block.atoms.some((atom) => atom.type !== "group" && atom.row.methodCode === "W-MOCK-11" || (atom.type === "group" && atom.methodCode === "W-MOCK-11")));
    const noted = page.placed.footnotes.some((note) => note.methodCode === "W-MOCK-11");
    assert(shows === noted, "harbour footnote page " + page.number);
  });

  const plot = logic.build(logic.configFromPreset("plot-matrix"));
  assert(plot.pages.length >= 4, "plot pages " + plot.pages.length);
  assert(plot.pages[0].sampleIds.join(",") === "MOCK-S-01,MOCK-S-02,MOCK-S-03,MOCK-S-04", "plot group " + plot.pages[0].sampleIds.join(","));
  assert(plot.pages.some((page) => page.sampleIds[0] === "MOCK-S-05"), "plot second group");
  assert(plot.pages.filter((page) => page.placed.resultNotes.some((note) => note.parameterCode === "FX-SM2")).length === 1, "plot result note count");
  assert(plot.pages.at(-1).signoff && plot.pages.slice(0, -1).every((page) => !page.signoff), "plot signoff");
  const wide = logic.build(Object.assign(logic.configFromPreset("plot-matrix"), { samplesPerPage: 6 }));
  assert(wide.pages[0].sampleIds.length === 6, "wide count " + wide.pages[0].sampleIds.length);
  assert(wide.issues.some((issue) => issue.code === "spp.stress"), "stress warning");

  const flush = logic.build(logic.configFromPreset("flush-limits"));
  const labels = (code, sampleId) => flush.rows.filter((row) => row.code === code).map((row) => logic.evaluateCell(flush.config, row, sampleId).label);
  assert(labels("FX-EC", "MOCK-DW-01")[0] === "Within fictional limit", "EC within");
  assert(labels("FX-ENT", "MOCK-DW-02")[0] === "Exceeds fictional limit", "ENT exceeds");
  assert(labels("FX-PBX", "MOCK-DW-01")[0].indexOf("unit") !== -1, "PBX unit");
  assert(labels("FX-CDX", "MOCK-DW-01")[0].indexOf("matrix") !== -1, "CDX matrix");
  assert(labels("FX-NIX", "MOCK-DW-01")[0].indexOf("jurisdiction") !== -1, "NIX jurisdiction");
  assert(labels("FX-CDX", "MOCK-DW-02")[0].indexOf("result not reported") !== -1, "CDX missing");
  assert(labels("FX-COLI", "MOCK-DW-01")[0].indexOf("below the reporting limit") !== -1, "COLI nd");
  assert(labels("FX-ZNX", "MOCK-DW-01")[0].indexOf("No applicable") !== -1, "ZNX none");
  const other = logic.normalize(Object.assign(logic.configFromPreset("flush-limits"), { mrl: { jurisdiction: "FICTION-OTHER", requireUnit: true, requireMatrix: true, requireJurisdiction: true } }));
  const row = flush.rows.find((item) => item.code === "FX-NIX");
  const ec = flush.rows.find((item) => item.code === "FX-EC");
  assert(logic.evaluateCell(other, row, "MOCK-DW-01").label === "Within fictional limit", "NIX other");
  assert(logic.evaluateCell(other, ec, "MOCK-DW-01").label.indexOf("jurisdiction") !== -1, "EC other");
  const unsafe = logic.normalize(Object.assign(logic.configFromPreset("flush-limits"), { mrl: { jurisdiction: "FICTION-NORDIC", requireUnit: false, requireMatrix: true, requireJurisdiction: true } }));
  assert(logic.evaluateCell(unsafe, ec, "MOCK-DW-01").kind === "blocked", "unsafe not blocked");
  assert(logic.validate(unsafe).some((issue) => issue.code === "mrl.unsafe"), "unsafe code");

  const basin = logic.build(logic.configFromPreset("basin-groups"));
  assert(basin.pages.some((page) => page.placed.testNotes.some((note) => note.sampleId === "MOCK-GW-01" && note.testCode === "GW-MET")), "basin test note");
  assert(basin.pages.every((page) => page.placed.testNotes.every((note) => note.sampleId !== "MOCK-GW-02")), "basin test note leaked");
  assert(basin.pages.some((page) => page.sampleIds.indexOf("MOCK-GW-02") !== -1 && page.placed.sampleNotes.some((note) => note.sampleId === "MOCK-GW-02")), "basin sample note");

  const left = logic.exportDocument(logic.build(logic.configFromPreset("harbour-vertical")));
  const right = logic.exportDocument(logic.build(logic.configFromPreset("plot-matrix")));
  assert(left.snapshotRef.id === right.snapshotRef.id, "snapshot ref diverged");
  assert(left.layout.mode !== right.layout.mode, "modes did not diverge");
  assert(left.pagination.pdf === false && left.pagination.simulated === true, "pdf flag");
  assert(left.selection.sampleIds[0] !== right.selection.sampleIds[0], "selection did not change");

  const bad = logic.build(Object.assign(logic.configFromPreset("plot-matrix"), { rowsPerPage: 2, domains: Object.assign({}, logic.configFromPreset("plot-matrix").domains, { sample: false }) }));
  assert(bad.withheld, "unsafe matrix was still drawn");
  assert(bad.pages.length === 0, "withheld still had pages");
  console.log("unit ok", {
    harbour: harbour.pages.length,
    basin: basin.pages.length,
    plot: plot.pages.length,
    flush: flush.pages.length
  });
}

function sleep(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

async function waitFor(url, attempts = 40) {
  for (let i = 0; i < attempts; i += 1) {
    try {
      const response = await fetch(url);
      if (response.ok) return;
    } catch (error) {
      /* server still starting */
    }
    await sleep(150);
  }
  throw new Error("timed out waiting for " + url);
}

async function browserCheck() {
  const port = 8771;
  const debugPort = 9223;
  const profile = "/tmp/nordic-chrome-profile";
  fs.rmSync(profile, { recursive: true, force: true });
  const server = spawn("python3", ["-m", "http.server", String(port), "--directory", root], { stdio: "ignore" });
  const chrome = spawn("/usr/bin/google-chrome", [
    "--headless=new",
    "--no-sandbox",
    "--disable-gpu",
    "--disable-dev-shm-usage",
    "--no-first-run",
    "--no-default-browser-check",
    `--user-data-dir=${profile}`,
    `--remote-debugging-port=${debugPort}`,
    "about:blank"
  ], { stdio: "ignore" });
  try {
    await waitFor(`http://127.0.0.1:${port}/index.html`);
    await waitFor(`http://127.0.0.1:${debugPort}/json/version`);
    const version = await fetch(`http://127.0.0.1:${debugPort}/json/version`).then((response) => response.json());
    const ws = new WebSocket(version.webSocketDebuggerUrl);
    await new Promise((resolve, reject) => {
      ws.addEventListener("open", resolve);
      ws.addEventListener("error", reject);
    });
    let seq = 0;
    const pending = new Map();
    const exceptions = [];
    ws.addEventListener("message", (event) => {
      const message = JSON.parse(event.data);
      if (message.id && pending.has(message.id)) {
        const waiter = pending.get(message.id);
        pending.delete(message.id);
        if (message.error) waiter.reject(new Error(JSON.stringify(message.error)));
        else waiter.resolve(message.result);
      } else if (message.method === "Runtime.exceptionThrown") {
        exceptions.push(message.params.exceptionDetails.text || "exception");
      }
    });
    const send = (method, params = {}, sessionId) => {
      const id = ++seq;
      const payload = { id, method, params };
      if (sessionId) payload.sessionId = sessionId;
      ws.send(JSON.stringify(payload));
      return new Promise((resolve, reject) => pending.set(id, { resolve, reject }));
    };
    const created = await send("Target.createTarget", { url: `http://127.0.0.1:${port}/index.html?selfcheck=1` });
    const attached = await send("Target.attachToTarget", { targetId: created.targetId, flatten: true });
    const sessionId = attached.sessionId;
    await send("Page.enable", {}, sessionId);
    await send("Runtime.enable", {}, sessionId);
    let report = null;
    for (let i = 0; i < 80; i += 1) {
      try {
        const evaluated = await send("Runtime.evaluate", {
          expression: "window.__nordicSelfcheck ? JSON.stringify(window.__nordicSelfcheck) : ''",
          returnByValue: true
        }, sessionId);
        const value = evaluated.result && evaluated.result.value;
        if (value) {
          report = JSON.parse(value);
          if (report.done) break;
        }
      } catch (error) {
        /* navigation between selfcheck phases drops the call */
      }
      await sleep(250);
    }
    if (!report || !report.done) throw new Error("selfcheck did not finish");
    if (!report.ok) {
      throw new Error("selfcheck failed\n" + report.failures.join("\n") + (exceptions.length ? "\nexceptions: " + exceptions.join(" | ") : ""));
    }
    if (exceptions.length) throw new Error("browser exceptions: " + exceptions.join(" | "));
    console.log("browser ok");
    ws.close();
  } finally {
    server.kill("SIGTERM");
    chrome.kill("SIGTERM");
  }
}

unit();
await browserCheck();
console.log("nordic checks passed");
