# Signatures

The approval block is a selected record, not a signature.

`Seed/catalogs/signatories.json` has one block, `sig-demo-reviewer`. A template stores that id. At render time the app loads the matching display name, role, statement, and the asset key `demo-mark`. The asset is `Assets/signatories/demo-mark.svg`, a drawn rectangle and the words “DEMO MARK / Not a signature”. It is not a scan of handwriting and not an accreditation mark.

The catalog text `approval.demo-block` is section-scoped. Its placeholders (`{{signatoryName}}`, `{{signatoryRole}}`, `{{signatoryStatement}}`, `{{sectionTitle}}`, `{{reportNumber}}`) are filled only while that text is resolved. They are not applied to the rest of the HTML. A disclaimer cannot use a signatory token; the resolver rejects it as outside the disclaimer scope.

If the approval section is enabled and the id or the asset key is unknown, validation fails and no PDF is published.

## What this is not

Nothing in the pipeline computes a CMS/PKCS#7 signature, a PAdES signature, or a trusted timestamp. `NoCryptographicSigner.AfterRenderAsync` receives the PDF bytes and returns `CryptographicSignatureApplied = false` plus a sentence that is stored on the job. The job page shows that flag.

A production hook would run after the PDF bytes and the SHA-256 exist: take those bytes, sign them with a key held outside this app, and store the signature next to the artifact without changing the rendered pages. Doing that inside the HTML, or by drawing a person’s name in a script font, would still not be a signature. This prototype does not do either of those things.

The banner “SYNTHETIC DATA / NOT A VALID LABORATORY CERTIFICATE” is printed in the body and in the PDF header on every page. The admin screens cannot turn it off.
