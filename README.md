# SSRS replacement — Northline lab report prototypes

Open **[README.html](README.html)** for the full write-up (what we tried to achieve, architecture, screenshots, how to run).

Quick links:

| Path | What |
| --- | --- |
| [`prototype/`](prototype/) | ASP.NET Core Razor + Playwright report desk |
| [`admin-mock/`](admin-mock/) | Browser-only layout studio (localStorage) |
| [`docs/research.md`](docs/research.md) | Research notes and recommendations |
| [`docs/sample-output/`](docs/sample-output/) | Generated sample PDFs |
| [`media/`](media/) | Walkthrough videos |
| [`screenshots/`](screenshots/) | UI screenshots |

### Run the report desk

```bash
cd prototype
dotnet run --project src/LabReport.Web --urls http://127.0.0.1:5891
```

### Run the layout studio

```bash
python3 -m http.server 8766 --directory admin-mock
```

Then open http://127.0.0.1:8766/

Private repository. All report data is synthetic and is not a laboratory certificate.
