/* Layout rules. Errors block a coherent preview story; warnings are advisory. */
function validateType(reportType) {
  const issues = [];
  const sections = reportType.sections || [];
  const enabled = (domain) => sections.some((section) => section.enabled && section.domain === domain);

  if (!reportType.name || !reportType.name.trim()) {
    issues.push(error("type.name", "The report type needs a name."));
  }
  const spp = Number(reportType.samplesPerPage);
  if (!Number.isInteger(spp) || spp < 1 || spp > 12) {
    issues.push(error("type.pagination", "Samples per page must be a whole number from 1 to 12."));
  }

  sections.forEach((section) => {
    if (section.required && !section.enabled) {
      issues.push(error("section.required", `${section.title} is required for this report type, but it is turned off.`));
    }
    if (section.enabled && section.columns && section.columns.every((column) => !column.visible)) {
      issues.push(error("section.columns", `${section.title} has every column hidden.`));
    }
    if (section.rules && section.rules.matrixEquals && !/^[a-z0-9-]{1,40}$/i.test(section.rules.matrixEquals)) {
      issues.push(error("section.matrix", `${section.title} has a matrix rule that is not a short token.`));
    }
  });

  if (enabled("mrl") && !enabled("result")) {
    issues.push(error("mrl.needs-result", "MRL comparison is on, but Results are not in this layout. A limit cannot be judged without a result."));
  }
  if (enabled("mrl") && !enabled("parameter")) {
    issues.push(error("mrl.needs-parameter", "MRL rows belong to a parameter. Turn Parameters on, or turn MRL off."));
  }
  if (enabled("result") && !enabled("test")) {
    issues.push(error("result.needs-test", "A result has no parent test in this layout."));
  }
  if (enabled("result") && !enabled("parameter")) {
    issues.push(error("result.needs-parameter", "A result has no parameter parent in this layout."));
  }
  if (enabled("parameter") && !enabled("test")) {
    issues.push(error("parameter.needs-test", "Parameters are on, but Tests are not."));
  }
  if (enabled("test") && !enabled("sample")) {
    issues.push(error("test.needs-sample", "Tests are on, but Samples are not."));
  }
  if ((enabled("sample") || enabled("test") || enabled("result")) && !enabled("report")) {
    issues.push(error("report.missing", "The report header is off while child sections are on."));
  }
  if (enabled("accreditation") && !enabled("test")) {
    issues.push(error("accreditation.needs-test", "Accreditation marks hang off a test or method, and Tests are off."));
  }

  sections.filter((section) => section.enabled && (section.domain === "comment" || section.domain === "conclusion")).forEach((section) => {
    const scopes = section.scopes || [];
    if (scopes.length === 0) {
      issues.push(error("text.scope", `${section.title} has no scope. Choose report, sample, test, or result.`));
    }
    scopes.forEach((scope) => {
      const parentOn = scope === "report" ? enabled("report") : enabled(scope === "result" ? "result" : scope);
      if (!parentOn) {
        issues.push(error(
          "text.parent",
          `${section.title} is scoped to ${scope}, but that parent is not in the layout.`
        ));
      }
    });
  });

  if (enabled("test") && sections.some((section) => section.enabled && section.domain === "test" && section.rules.hideIfNotAccredited) && enabled("accreditation")) {
    issues.push(warn("accreditation.hidden", "Tests are hidden when they are not accredited, so the accreditation footnote has nothing left to mark."));
  }

  return issues;
}

function error(code, message) {
  return { level: "error", code, message };
}

function warn(code, message) {
  return { level: "warning", code, message };
}
