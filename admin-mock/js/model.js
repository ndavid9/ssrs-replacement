/* Domain catalog and layout document. Named feeds only — never a URL or SQL string. */
const LAYOUT_SCHEMA_VERSION = "1.0";
const STORAGE_KEY = "northline-layout-studio-v1";

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

const DOMAINS = [
  { id: "report", label: "Report", multiplicity: "1", edge: "Root of the document", parents: [] },
  { id: "sample", label: "Sample", multiplicity: "1..*", edge: "Report 1 → many Sample", parents: ["report"] },
  { id: "test", label: "Test", multiplicity: "1..*", edge: "Sample 1 → many Test", parents: ["sample"] },
  { id: "parameter", label: "Parameter", multiplicity: "1..*", edge: "Test 1 → many Parameter", parents: ["test"] },
  { id: "result", label: "Result", multiplicity: "0..1", edge: "One result slot per sample, test, and parameter", parents: ["parameter", "test", "sample"] },
  { id: "comment", label: "Comments", multiplicity: "0..*", edge: "Attach at report, sample, test, or result", parents: ["report", "sample", "test", "result"] },
  { id: "conclusion", label: "Conclusions", multiplicity: "0..*", edge: "Attach at report, sample, test, or result", parents: ["report", "sample", "test", "result"] },
  { id: "mrl", label: "MRL", multiplicity: "0..*", edge: "Many fictional limits per parameter, matched on matrix, jurisdiction, unit, and date", parents: ["parameter"] },
  { id: "accreditation", label: "Accreditation", multiplicity: "0..*", edge: "Marks on a test or method", parents: ["test"] }
];

const COLUMN_LIBRARY = {
  report: [
    ["reportNumber", "Report number"],
    ["title", "Title"],
    ["client", "Client"],
    ["reportDate", "Report date"],
    ["matrix", "Matrix"],
    ["jurisdiction", "Jurisdiction"]
  ],
  sample: [
    ["sampleId", "Sample id"],
    ["description", "Description"],
    ["matrix", "Matrix"],
    ["received", "Received"]
  ],
  test: [
    ["testCode", "Test code"],
    ["methodName", "Method"],
    ["accredited", "Accredited"]
  ],
  parameter: [
    ["name", "Parameter"],
    ["code", "Code"],
    ["unit", "Unit"],
    ["lod", "LOD"],
    ["loq", "LOQ"]
  ],
  result: [
    ["display", "Result"],
    ["qualifier", "Qualifier"],
    ["unit", "Unit"]
  ],
  comment: [
    ["body", "Comment"],
    ["anchor", "Attached to"]
  ],
  conclusion: [
    ["body", "Conclusion"],
    ["anchor", "Attached to"]
  ],
  mrl: [
    ["limit", "Fictional limit"],
    ["unit", "Limit unit"],
    ["jurisdiction", "Jurisdiction"],
    ["evaluation", "Evaluation"],
    ["effective", "Effective"]
  ],
  accreditation: [
    ["methodName", "Method"],
    ["status", "Status"],
    ["mark", "Mark"]
  ]
};

const SCOPE_OPTIONS = ["report", "sample", "test", "result"];

function columnsFor(domain) {
  return (COLUMN_LIBRARY[domain] || []).map(([key, label]) => ({ key, label, visible: true }));
}

function uid(prefix) {
  return prefix + "-" + Math.random().toString(36).slice(2, 8);
}

function sectionTemplate(domain, order, patch) {
  const base = {
    instanceId: uid(domain),
    domain,
    title: DOMAINS.find((item) => item.id === domain).label,
    enabled: true,
    required: domain === "report" || domain === "sample",
    order,
    columns: columnsFor(domain),
    scopes: domain === "comment" || domain === "conclusion" ? ["report", "sample"] : [],
    rules: { showWhenHasData: false, hideIfNotAccredited: false, matrixEquals: "" },
    mrl: domain === "mrl"
      ? { jurisdiction: "FICTION-ZONE-7", requireUnitMatch: true, showNotEvaluated: true }
      : null,
    accreditation: domain === "accreditation"
      ? { footnoteNonAccredited: true, mark: "†" }
      : null
  };
  return Object.assign(base, patch || {});
}

function freshType(name) {
  const id = name.toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-|-$/g, "") || uid("type");
  const domains = ["report", "sample", "test", "parameter", "result", "comment", "conclusion", "mrl", "accreditation"];
  const enabled = new Set(["report", "sample", "test", "parameter", "result", "comment", "conclusion"]);
  return {
    id,
    name,
    samplesPerPage: 2,
    sections: domains.map((domain, index) => sectionTemplate(domain, (index + 1) * 10, {
      enabled: enabled.has(domain),
      required: domain === "report" || domain === "sample" || domain === "result"
    }))
  };
}

function seedDocument() {
  const food = freshType("Food Export");
  food.id = "food-export";
  food.samplesPerPage = 2;
  food.sections.forEach((section) => {
    section.enabled = true;
    if (section.domain === "comment") section.scopes = ["report", "sample"];
    if (section.domain === "conclusion") section.scopes = ["report", "sample"];
  });

  const water = freshType("Water Compliance");
  water.id = "water-compliance";
  water.samplesPerPage = 1;
  water.sections.forEach((section) => {
    if (section.domain === "sample") section.rules.matrixEquals = "water";
    if (section.domain === "comment") section.scopes = ["sample", "result"];
    if (section.domain === "conclusion") section.scopes = ["report"];
    if (section.domain === "accreditation") section.enabled = true;
  });

  const qc = freshType("Internal QC");
  qc.id = "internal-qc";
  qc.samplesPerPage = 3;
  qc.sections.forEach((section) => {
    if (section.domain === "mrl" || section.domain === "accreditation") section.enabled = false;
    if (section.domain === "comment") section.scopes = ["sample"];
    if (section.domain === "conclusion") {
      section.enabled = false;
      section.scopes = ["sample"];
    }
    if (section.domain === "result") section.required = false;
  });

  return {
    schemaVersion: LAYOUT_SCHEMA_VERSION,
    feeds: Object.assign({}, FEEDS),
    reportTypes: [food, water, qc]
  };
}

function clone(value) {
  return JSON.parse(JSON.stringify(value));
}

function loadDocument() {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return seedDocument();
    const parsed = JSON.parse(raw);
    if (!parsed || parsed.schemaVersion !== LAYOUT_SCHEMA_VERSION || !Array.isArray(parsed.reportTypes)) {
      return seedDocument();
    }
    parsed.feeds = Object.assign({}, FEEDS);
    return parsed;
  } catch {
    return seedDocument();
  }
}

function saveDocument(doc) {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(doc));
}

function domainById(id) {
  return DOMAINS.find((item) => item.id === id);
}
