# Northline layout studio

A static mock for curating report layouts. It does not call a server. The document is stored in `localStorage` under `northline-layout-studio-v1`.

Open `index.html` in a browser, or serve this folder:

```bash
python3 -m http.server 8766 --directory admin-mock
```

Then open http://127.0.0.1:8766/

When the report desk is running, the same files are also at http://127.0.0.1:5891/admin-mock/ after a restart that includes this folder.

Export writes `northline-layout.json` with `schemaVersion`, named feed contracts, and the report-type section tree. Feeds are fixed names such as `report-snapshot-v1` and `mrl-fictional-v1`. The studio will not store a URL or a SQL string.

Reset demo restores Food Export, Water Compliance, and Internal QC.
