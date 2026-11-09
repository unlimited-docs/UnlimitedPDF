# Demo images

Project-authored images for the demo app, released under Apache-2.0 with the rest of the repository (PR-008, PR-011).

## Origin

Every file below was generated on 2026-11-01 by [`Generate-DemoAssets.ps1`](Generate-DemoAssets.ps1). The script draws simple shapes with the Windows imaging APIs (WPF) and uses no fonts or third-party artwork. Run it again to recreate the files:

```powershell
powershell -NoProfile -STA -ExecutionPolicy Bypass -File Generate-DemoAssets.ps1
```

| File | Size | Format | Shows |
|---|---|---|---|
| `logo.png` | 160 x 160 px | PNG with alpha (transparent corners) | Document icon on a blue circle with an orange ring |
| `keyboard.jpg` | 96 x 64 px | Baseline RGB JPEG | Keyboard illustration |
| `dock.jpg` | 96 x 64 px | Baseline RGB JPEG | Docking station illustration |
| `cmyk-sample.jpg` | 128 x 96 px | Baseline CMYK JPEG with Adobe segment | Cyan, magenta, yellow, and black quadrants |

`html-invoice.html` is the HTML to PDF sample invoice, written by hand for the project on 2026-11-10 (the same text as the playground's invoice template, `UnlimitedPDF.Demos.BlazorWasm/Samples/HtmlTemplates.cs`). It shows `logo.png` from this folder.

The build copies the image files and the sample HTML to `Assets/` in the demo's output folder.
