/* Shared fictional snapshot for every Nordic preset.
   Invented values only. Not a laboratory certificate and not copied from a published report. */
(function () {
  const num = (value, mu) => ({ status: "numeric", value, mu: mu == null ? null : mu });
  const nd = () => ({ status: "notDetected" });
  const miss = () => ({ status: "missing" });
  const ids = (prefix, count) => Array.from({ length: count }, (_, i) => `${prefix}${String(i + 1).padStart(2, "0")}`);
  const pw = ids("MOCK-PW-", 3);
  const gw = ids("MOCK-GW-", 3);
  const soil = ids("MOCK-S-", 8);
  const hm = ids("MOCK-HM-", 2);
  const dw = ids("MOCK-DW-", 2);

  function pack(sampleIds, values) {
    const results = {};
    sampleIds.forEach((id, index) => {
      results[id] = values[index];
    });
    return results;
  }

  function parameter(spec) {
    return {
      code: spec.code,
      name: spec.name,
      unit: spec.unit,
      lor: spec.lor == null ? null : spec.lor,
      loq: spec.loq == null ? null : spec.loq,
      reference: spec.reference || null,
      calculated: Boolean(spec.calculated),
      results: spec.results
    };
  }

  function test(spec) {
    return {
      testCode: spec.testCode,
      title: spec.title,
      methodCode: spec.methodCode,
      methodName: spec.methodName,
      accredited: spec.accredited !== false,
      parameters: spec.parameters
    };
  }

  const samples = [
    { id: pw[0], matrix: "water", description: "Fictional harbour grab A", taken: "2026-05-12", detail: "surface" },
    { id: pw[1], matrix: "water", description: "Fictional harbour grab B", taken: "2026-05-12", detail: "mid-depth" },
    { id: pw[2], matrix: "water", description: "Fictional harbour grab C", taken: "2026-05-13", detail: "surface" },
    { id: gw[0], matrix: "groundwater", description: "Fictional basin point A", taken: "2026-05-04", detail: "screen 4–6 m" },
    { id: gw[1], matrix: "groundwater", description: "Fictional basin point B", taken: "2026-05-04", detail: "screen 8–10 m" },
    { id: gw[2], matrix: "groundwater", description: "Fictional basin point C", taken: "2026-05-05", detail: "screen 4–6 m" },
    ...soil.map((id, index) => ({
      id,
      matrix: "soil",
      description: `Fictional plot core ${index + 1}`,
      taken: "2026-04-28",
      detail: index % 2 === 0 ? "0.2–0.5 m" : "0.5–1.0 m"
    })),
    { id: hm[0], matrix: "hardened", description: "Fictional core piece A", taken: "2026-03-19", detail: "crushed portion" },
    { id: hm[1], matrix: "hardened", description: "Fictional core piece B", taken: "2026-03-19", detail: "crushed portion" },
    { id: dw[0], matrix: "distribution", description: "Fictional flush point A", taken: "2026-06-02", detail: "kitchen tap" },
    { id: dw[1], matrix: "distribution", description: "Fictional flush point B", taken: "2026-06-02", detail: "end of spur" }
  ];

  const matrices = {
    water: {
      label: "Water",
      tests: [
        test({
          testCode: "PW-SUL",
          title: "Fictional sulfonates",
          methodCode: "W-MOCK-04",
          methodName: "Fictional liquid chromatography screen",
          parameters: [
            parameter({ code: "FX-WS1", name: "Fictional sulfonate A", unit: "µg/L", lor: 0.01, loq: 0.03, results: pack(pw, [num(0.041, 0.009), nd(), num(0.07, 0.02)]) }),
            parameter({ code: "FX-WS2", name: "Fictional sulfonate B", unit: "µg/L", lor: 0.01, loq: 0.03, results: pack(pw, [num(0.22, 0.04), num(0.05, 0.01), nd()]) }),
            parameter({ code: "FX-WS3", name: "Fictional sulfonate C", unit: "µg/L", lor: 0.02, loq: 0.05, results: pack(pw, [num(0.08, 0.02), num(0.03, null), nd()]) }),
            parameter({ code: "FX-WS4", name: "Fictional sulfonate D", unit: "µg/L", lor: 0.02, loq: 0.05, results: pack(pw, [nd(), num(0.11, 0.03), miss()]) })
          ]
        }),
        test({
          testCode: "PW-CAR",
          title: "Fictional carboxylates",
          methodCode: "W-MOCK-05",
          methodName: "Fictional carboxylate screen",
          parameters: [
            parameter({ code: "FX-WC1", name: "Fictional carboxylate A", unit: "µg/L", lor: 0.01, loq: 0.04, results: pack(pw, [num(0.06, 0.01), nd(), num(0.09, 0.02)]) }),
            parameter({ code: "FX-WC2", name: "Fictional carboxylate B", unit: "µg/L", lor: 0.01, loq: 0.04, results: pack(pw, [nd(), nd(), num(0.04, 0.01)]) }),
            parameter({ code: "FX-WC3", name: "Fictional carboxylate C", unit: "µg/L", lor: 0.03, loq: 0.08, results: pack(pw, [num(0.15, 0.03), miss(), nd()]) }),
            parameter({ code: "FX-WC4", name: "Fictional carboxylate D", unit: "µg/L", lor: 0.03, loq: 0.08, results: pack(pw, [num(0.02, 0.01), num(0.05, 0.01), num(0.01, 0.005)]) })
          ]
        }),
        test({
          testCode: "PW-SUM",
          title: "Fictional sums",
          methodCode: "W-MOCK-04",
          methodName: "Fictional liquid chromatography screen",
          parameters: [
            parameter({ code: "FX-WSUM", name: "Fictional sulfonate sum", unit: "µg/L", lor: null, loq: null, calculated: true, results: pack(pw, [num(0.34, null), num(0.08, null), num(0.07, null)]) }),
            parameter({ code: "FX-W11", name: "Fictional eleven-analogue sum", unit: "µg/L", lor: null, loq: null, calculated: true, results: pack(pw, [num(0.51, null), num(0.19, null), num(0.12, null)]) })
          ]
        }),
        test({
          testCode: "PW-ADD",
          title: "Fictional add-on screen",
          methodCode: "W-MOCK-11",
          methodName: "Fictional unaccredited add-on",
          accredited: false,
          parameters: [
            parameter({ code: "FX-WA1", name: "Fictional add-on analyte A", unit: "µg/L", lor: 0.05, loq: 0.1, results: pack(pw, [num(0.2, 0.05), nd(), miss()]) }),
            parameter({ code: "FX-WA2", name: "Fictional add-on analyte B", unit: "µg/L", lor: 0.05, loq: 0.1, results: pack(pw, [nd(), num(0.3, 0.06), num(0.12, 0.03)]) })
          ]
        })
      ]
    },
    groundwater: {
      label: "Groundwater",
      tests: [
        test({
          testCode: "GW-MET",
          title: "Fictional metals",
          methodCode: "G-MOCK-02",
          methodName: "Fictional metals digest",
          parameters: [
            parameter({ code: "FX-GA", name: "Fictional arsenic analogue", unit: "µg/L", lor: 0.5, loq: 1, results: pack(gw, [num(11, 1.4), num(4.4, 0.6), num(7.2, 0.9)]) }),
            parameter({ code: "FX-GB", name: "Fictional barium analogue", unit: "µg/L", lor: 1, loq: 2, results: pack(gw, [num(70, 8), num(24, 3), nd()]) }),
            parameter({ code: "FX-GP", name: "Fictional lead analogue", unit: "µg/L", lor: 0.5, loq: 1, results: pack(gw, [num(6.5, 0.8), num(1.1, 0.2), num(3.3, null)]) }),
            parameter({ code: "FX-GC", name: "Fictional copper analogue", unit: "µg/L", lor: 0.5, loq: 1, results: pack(gw, [num(18, 2), nd(), num(9, 1)]) }),
            parameter({ code: "FX-GZ", name: "Fictional zinc analogue", unit: "µg/L", lor: 1, loq: 3, results: pack(gw, [num(44, null), num(12, 2), miss()]) })
          ]
        }),
        test({
          testCode: "GW-ALI",
          title: "Fictional aliphatics",
          methodCode: "G-MOCK-04",
          methodName: "Fictional hydrocarbon screen",
          parameters: [
            parameter({ code: "FX-GL1", name: "Fictional aliphatic light", unit: "µg/L", lor: 10, loq: 20, results: pack(gw, [nd(), num(30, 8), num(18, 5)]) }),
            parameter({ code: "FX-GL2", name: "Fictional aliphatic mid", unit: "µg/L", lor: 10, loq: 20, results: pack(gw, [num(42, 9), num(16, 4), nd()]) }),
            parameter({ code: "FX-GL3", name: "Fictional aliphatic heavy", unit: "µg/L", lor: 10, loq: 25, results: pack(gw, [num(25, 6), miss(), num(33, 7)]) }),
            parameter({ code: "FX-GLS", name: "Fictional aliphatic sum", unit: "µg/L", calculated: true, results: pack(gw, [num(67, null), num(46, null), num(51, null)]) })
          ]
        }),
        test({
          testCode: "GW-ARO",
          title: "Fictional aromatics",
          methodCode: "G-MOCK-07",
          methodName: "Fictional aromatic screen",
          accredited: false,
          parameters: [
            parameter({ code: "FX-GR1", name: "Fictional aromatic light", unit: "µg/L", lor: 5, loq: 10, results: pack(gw, [num(8, 2), nd(), num(6, 1)]) }),
            parameter({ code: "FX-GR2", name: "Fictional aromatic mid", unit: "µg/L", lor: 5, loq: 10, results: pack(gw, [nd(), num(14, 3), num(9, 2)]) }),
            parameter({ code: "FX-GRS", name: "Fictional aromatic sum", unit: "µg/L", calculated: true, results: pack(gw, [num(8, null), num(14, null), num(15, null)]) })
          ]
        }),
        test({
          testCode: "GW-VOL",
          title: "Fictional volatiles",
          methodCode: "G-MOCK-08",
          methodName: "Fictional volatile screen",
          parameters: [
            parameter({ code: "FX-GV1", name: "Fictional volatile A", unit: "µg/L", lor: 0.2, loq: 0.5, results: pack(gw, [nd(), nd(), num(0.6, 0.1)]) }),
            parameter({ code: "FX-GV2", name: "Fictional volatile B", unit: "µg/L", lor: 0.2, loq: 0.5, results: pack(gw, [num(1.4, 0.2), num(0.4, 0.1), nd()]) }),
            parameter({ code: "FX-GV3", name: "Fictional volatile C", unit: "µg/L", lor: 0.5, loq: 1, results: pack(gw, [num(2.2, 0.3), miss(), num(0.8, 0.1)]) }),
            parameter({ code: "FX-GVS", name: "Fictional volatile sum", unit: "µg/L", calculated: true, results: pack(gw, [num(3.6, null), num(0.4, null), num(1.4, null)]) })
          ]
        })
      ]
    },
    soil: {
      label: "Soil",
      tests: [
        test({
          testCode: "S-PHY",
          title: "Fictional physical",
          methodCode: "S-MOCK-01",
          methodName: "Fictional physical prep",
          parameters: [
            parameter({
              code: "FX-PHY1",
              name: "Fictional moisture",
              unit: "%",
              lor: 0.1,
              loq: 0.5,
              results: pack(soil, [18, 22, 15, 19, 25, 11, 16, 20].map((value) => num(value, 1)))
            }),
            parameter({
              code: "FX-PHY2",
              name: "Fictional dry fraction",
              unit: "%",
              lor: 0.1,
              loq: 0.5,
              results: pack(soil, [num(71, 1), num(64, 1), num(83, 1), num(58, 1), num(76, 1), num(69, 1), miss(), num(80, 1)])
            })
          ]
        }),
        test({
          testCode: "S-HYD",
          title: "Fictional hydrocarbons",
          methodCode: "S-MOCK-03",
          methodName: "Fictional solvent screen",
          parameters: [
            parameter({ code: "FX-SH1", name: "Fictional hydrocarbon light", unit: "mg/kg", lor: 40, loq: 80, results: pack(soil, [num(20, 5), num(120, 15), nd(), num(44, 6), num(90, 10), nd(), num(15, 4), num(200, 20)]) }),
            parameter({ code: "FX-SH2", name: "Fictional hydrocarbon mid", unit: "mg/kg", lor: 40, loq: 80, results: pack(soil, [num(12, 4), num(44, 6), nd(), num(60, 8), num(18, 4), num(90, 11), num(33, 5), num(41, 6)]) }),
            parameter({ code: "FX-SH3", name: "Fictional hydrocarbon heavy", unit: "mg/kg", lor: 40, loq: 80, results: pack(soil, [num(8, 3), num(15, 4), num(22, 5), miss(), num(48, 7), nd(), num(11, 3), num(19, 4)]) }),
            parameter({ code: "FX-SH4", name: "Fictional hydrocarbon residue", unit: "mg/kg", lor: 40, loq: 80, results: pack(soil, [nd(), nd(), num(36, 6), num(28, 5), num(16, 4), num(55, 8), num(42, 6), num(9, 3)]) }),
            parameter({ code: "FX-SH5", name: "Fictional hydrocarbon sum", unit: "mg/kg", calculated: true, results: pack(soil, [num(40, null), num(160, null), num(36, null), num(90, null), num(110, null), num(70, null), num(30, null), num(220, null)]) })
          ]
        }),
        test({
          testCode: "S-MET",
          title: "Fictional metals",
          methodCode: "S-MOCK-08",
          methodName: "Fictional soil digest",
          parameters: [
            parameter({ code: "FX-SM1", name: "Fictional lead analogue", unit: "mg/kg", lor: 1, loq: 2, results: pack(soil, [12, 4, 30, 8, 15, 2, 18, 6].map((value) => num(value, 1))) }),
            parameter({ code: "FX-SM2", name: "Fictional copper analogue", unit: "mg/kg", lor: 1, loq: 2, results: pack(soil, [40, 10, 55, 12, 8, 60, 22, 15].map((value) => num(value, 2))) }),
            parameter({ code: "FX-SM3", name: "Fictional zinc analogue", unit: "mg/kg", lor: 1, loq: 3, results: pack(soil, [80, 20, 15, 40, 100, 12, 33, 28].map((value) => num(value, 3))) }),
            parameter({ code: "FX-SM4", name: "Fictional nickel analogue", unit: "mg/kg", lor: 1, loq: 2, results: pack(soil, [nd(), num(6, 1), num(8, 1), num(4, 1), nd(), num(11, 1), num(3, 1), num(9, 1)]) }),
            parameter({ code: "FX-SM5", name: "Fictional cadmium analogue", unit: "mg/kg", lor: 0.2, loq: 0.5, results: pack(soil, [num(0.3, 0.1), nd(), num(0.6, 0.1), num(0.2, 0.05), num(0.4, 0.1), miss(), num(0.25, 0.05), num(0.7, 0.1)]) })
          ]
        })
      ]
    },
    hardened: {
      label: "Hardened material",
      tests: [
        test({
          testCode: "HM-PHY",
          title: "Fictional physical",
          methodCode: "H-MOCK-01",
          methodName: "Fictional solids prep",
          parameters: [
            parameter({ code: "FX-HD1", name: "Fictional dry fraction", unit: "%", lor: 0.1, loq: 0.5, results: pack(hm, [num(81.2, 1.1), num(74.4, 1.0)]) }),
            parameter({ code: "FX-HD2", name: "Fictional moisture", unit: "%", lor: 0.1, loq: 0.5, results: pack(hm, [num(6.4, 0.3), num(8.8, 0.4)]) })
          ]
        }),
        test({
          testCode: "HM-MET",
          title: "Fictional metals",
          methodCode: "H-MOCK-02",
          methodName: "Fictional solids digest",
          parameters: [
            parameter({ code: "FX-HM1", name: "Fictional arsenic analogue", unit: "mg/kg", lor: 0.4, loq: 0.8, results: pack(hm, [num(2.2, 0.4), num(1.4, 0.3)]) }),
            parameter({ code: "FX-HM2", name: "Fictional lead analogue", unit: "mg/kg", lor: 0.4, loq: 0.8, results: pack(hm, [num(5.5, 0.7), num(3.1, 0.4)]) }),
            parameter({ code: "FX-HM3", name: "Fictional cadmium analogue", unit: "mg/kg", lor: 0.2, loq: 0.4, results: pack(hm, [miss(), num(0.6, 0.1)]) }),
            parameter({ code: "FX-HM4", name: "Fictional copper analogue", unit: "mg/kg", lor: 0.4, loq: 0.8, results: pack(hm, [num(7, 1), num(4.2, 0.6)]) }),
            parameter({ code: "FX-HM5", name: "Fictional chromium analogue", unit: "mg/kg", lor: 0.4, loq: 0.8, results: pack(hm, [num(4, 0.8), num(6.6, 1)]) }),
            parameter({ code: "FX-HM6", name: "Fictional nickel analogue", unit: "mg/kg", lor: 0.4, loq: 0.8, results: pack(hm, [num(3.3, 0.5), num(2.4, 0.4)]) }),
            parameter({ code: "FX-HM7", name: "Fictional zinc analogue", unit: "mg/kg", lor: 1, loq: 2, results: pack(hm, [num(18, 2), num(27, 3)]) })
          ]
        }),
        test({
          testCode: "HM-HEX",
          title: "Fictional hexavalent screen",
          methodCode: "H-MOCK-09",
          methodName: "Fictional hexavalent screen",
          accredited: false,
          parameters: [
            parameter({ code: "FX-HX1", name: "Fictional hexavalent analogue", unit: "mg/kg", lor: 0.2, loq: 0.4, results: pack(hm, [num(0.4, 0.1), num(0.7, 0.15)]) }),
            parameter({ code: "FX-HX2", name: "Fictional hexavalent check", unit: "mg/kg", lor: 0.2, loq: 0.4, results: pack(hm, [nd(), num(0.3, 0.08)]) })
          ]
        })
      ]
    },
    distribution: {
      label: "Distribution water",
      tests: [
        test({
          testCode: "D-FLD",
          title: "Fictional field",
          methodCode: "D-MOCK-01",
          methodName: "Fictional field meter",
          parameters: [
            parameter({ code: "FX-TMP", name: "Fictional field temperature", unit: "°C", lor: null, loq: null, results: pack(dw, [num(12.4, 0.2), num(9.1, 0.2)]) })
          ]
        }),
        test({
          testCode: "D-MIC",
          title: "Fictional microbiology",
          methodCode: "D-MOCK-02",
          methodName: "Fictional plate count",
          parameters: [
            parameter({ code: "FX-EC", name: "Fictional coliform analogue", unit: "CFU/100 ml", lor: 1, loq: 1, reference: "Fictional guidance text, layout only", results: pack(dw, [num(0, null), num(4, null)]) }),
            parameter({ code: "FX-ENT", name: "Fictional enterococci analogue", unit: "CFU/100 ml", lor: 1, loq: 1, reference: "Fictional guidance text, layout only", results: pack(dw, [num(80, null), num(260, null)]) }),
            parameter({ code: "FX-COLI", name: "Fictional colony analogue", unit: "CFU/ml", lor: 1, loq: 1, results: pack(dw, [nd(), num(12, null)]) })
          ]
        }),
        test({
          testCode: "D-CHM",
          title: "Fictional chemistry",
          methodCode: "D-MOCK-05",
          methodName: "Fictional water metals screen",
          parameters: [
            parameter({ code: "FX-PBX", name: "Fictional lead analogue", unit: "µg/L", lor: 0.5, loq: 1, results: pack(dw, [num(3.2, 0.4), num(8.1, 0.9)]) }),
            parameter({ code: "FX-CDX", name: "Fictional cadmium analogue", unit: "µg/L", lor: 0.2, loq: 0.5, results: pack(dw, [num(0.4, 0.1), miss()]) }),
            parameter({ code: "FX-NIX", name: "Fictional nickel analogue", unit: "µg/L", lor: 0.5, loq: 1, results: pack(dw, [num(6, 1), num(11, 1)]) }),
            parameter({ code: "FX-ZNX", name: "Fictional zinc analogue", unit: "µg/L", lor: 1, loq: 3, results: pack(dw, [num(40, 4), num(55, 5)]) }),
            parameter({ code: "FX-CUX", name: "Fictional copper analogue", unit: "µg/L", lor: 2, loq: 4, results: pack(dw, [nd(), num(22, 3)]) }),
            parameter({ code: "FX-ASX", name: "Fictional arsenic analogue", unit: "µg/L", lor: 0.5, loq: 1, results: pack(dw, [num(14, 2), num(2, 0.4)]) })
          ]
        })
      ]
    }
  };

  const limit = (parameterCode, matrix, jurisdiction, unit, value) => ({
    parameterCode,
    matrix,
    jurisdiction,
    unit,
    limit: value
  });

  const limits = [
    limit("FX-WS1", "water", "FICTION-NORDIC", "µg/L", 0.1),
    limit("FX-WS2", "water", "FICTION-NORDIC", "µg/L", 0.1),
    limit("FX-WS3", "water", "FICTION-NORDIC", "mg/L", 0.05),
    limit("FX-GA", "groundwater", "FICTION-NORDIC", "µg/L", 20),
    limit("FX-GP", "groundwater", "FICTION-NORDIC", "mg/L", 0.01),
    limit("FX-SH1", "soil", "FICTION-NORDIC", "mg/kg", 80),
    limit("FX-SM1", "soil", "FICTION-NORDIC", "g/kg", 0.02),
    limit("FX-HM2", "hardened", "FICTION-NORDIC", "mg/kg", 10),
    limit("FX-EC", "distribution", "FICTION-NORDIC", "CFU/100 ml", 180),
    limit("FX-ENT", "distribution", "FICTION-NORDIC", "CFU/100 ml", 90),
    limit("FX-COLI", "distribution", "FICTION-NORDIC", "CFU/ml", 40),
    limit("FX-PBX", "distribution", "FICTION-NORDIC", "mg/L", 0.01),
    limit("FX-CDX", "water", "FICTION-NORDIC", "µg/L", 5),
    limit("FX-NIX", "distribution", "FICTION-OTHER", "µg/L", 20),
    limit("FX-CUX", "distribution", "FICTION-NORDIC", "µg/L", 50),
    limit("FX-ASX", "distribution", "FICTION-NORDIC", "µg/L", 10)
  ];

  window.NordicSnapshot = {
    id: "nordic-snapshot-v1",
    feed: "report-snapshot-v1",
    report: {
      reportNumber: "MOCK-NORDIC-2042",
      title: "Fictional multi-matrix screen",
      client: "Northline Harbour Cooperative",
      site: "Fictional Basin Works",
      issued: "2026-06-18",
      issuer: "Northline Demo Desk"
    },
    signoff: {
      heading: "Fictional review block",
      name: "R. North",
      role: "Placeholder reviewer",
      statement: "Layout end-matter only. This is not a signature, not a qualification, and not for release."
    },
    samples,
    matrices,
    limits,
    comments: [
      { id: "c-report", scope: "report", body: "Report comment from the shared text catalog. Printed on the first simulated page only." },
      { id: "c-pw-result", scope: "result", sampleId: "MOCK-PW-01", parameterCode: "FX-WS1", body: "Result note bound to FX-WS1 on MOCK-PW-01." },
      { id: "c-gw-sample", scope: "sample", sampleId: "MOCK-GW-02", body: "Sample note bound to MOCK-GW-02." },
      { id: "c-gw-test", scope: "test", sampleId: "MOCK-GW-01", testCode: "GW-MET", body: "Test note bound to GW-MET on MOCK-GW-01." },
      { id: "c-s-sample-2", scope: "sample", sampleId: "MOCK-S-02", body: "Sample note bound to MOCK-S-02." },
      { id: "c-s-sample-6", scope: "sample", sampleId: "MOCK-S-06", body: "Sample note bound to MOCK-S-06." },
      { id: "c-s-result", scope: "result", sampleId: "MOCK-S-03", parameterCode: "FX-SM2", body: "Result note bound to FX-SM2 on MOCK-S-03." },
      { id: "c-hm-test", scope: "test", sampleId: "MOCK-HM-01", testCode: "HM-HEX", body: "Test note bound to HM-HEX on MOCK-HM-01." },
      { id: "c-dw-result", scope: "result", sampleId: "MOCK-DW-01", parameterCode: "FX-EC", body: "Result note bound to FX-EC on MOCK-DW-01." },
      { id: "c-dw-sample", scope: "sample", sampleId: "MOCK-DW-02", body: "Sample note bound to MOCK-DW-02." }
    ],
    conclusions: [
      { id: "k-report", scope: "report", body: "Report conclusion from the shared text catalog. Printed once, on the last simulated page, and not under each sample." },
      { id: "k-s-02", scope: "sample", sampleId: "MOCK-S-02", body: "Sample conclusion bound to MOCK-S-02." },
      { id: "k-dw-01", scope: "sample", sampleId: "MOCK-DW-01", body: "Sample conclusion bound to MOCK-DW-01." }
    ]
  };
})();
