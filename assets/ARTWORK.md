# UnlimitedPDF artwork (originals)

Released under Apache-2.0 with the rest of the repository (PR-008, PR-011).

Created with ChatGPT image generation for the project owner and downloaded on 2026-11-04 from the [shared asset chat](https://chatgpt.com/share/6aeb010e-7d90-83e8-b31b-ae43794edd92). This folder contains 12 PNG artwork files. On 2026-11-04, the GitHub banner and social card were edited with the built-in image generation tool to replace unsupported API examples in their code windows. Their surrounding designs and original dimensions were retained. The demo hero was also edited to use the current UnlimitedPDF Demos logo once, replacing the stacked logo and duplicate headline while retaining the subtitle and four document illustrations. Its 800- and 1200-pixel responsive copies were regenerated with `New-DerivedImages.ps1`. The remaining 9 files are unchanged originals. The separate download for the resized seven-file demo pack was unavailable, so original dimensions and sizes differ from those advertised for that ZIP.

These are source files. They live here, outside every served or packed folder, so no published site or package carries them (PR-013). The demo app, the package, and the readmes use the smaller copies listed under [Derived copies](#derived-copies). `generated-assets-manifest.json` records dimensions, sizes, source asset identifiers, and SHA-256 checksums. The two corrected banners also have edit records containing the exact generation prompts and displayed C# snippets. Both snippets were compiled and executed against the local `UnlimitedPDF.csproj` with .NET SDK 10.0.403, and their rendered text was visually checked. They use `AddPage()`, `AddText(...)`, and `Save(...)` as implemented in `PdfDocument.cs` and `PdfPageBuilder.cs`.

| File | Dimensions | Purpose |
|---|---|---|
| `originals/invoice-logo.png` | 2172 x 724 | Invoice demo logo |
| `originals/medical-report-logo.png` | 2172 x 724 | Medical report demo logo |
| `originals/receipt-logo.png` | 1983 x 793 | Receipt demo logo |
| `originals/certificate-seal.png` | 1254 x 1254 | Certificate demo seal |
| `originals/certificate-signature.png` | 2172 x 724 | Fictional certificate demo signature |
| `branding/favicon.png` | 1254 x 1254 | Original favicon artwork |
| `branding/unlimitedpdf-logo.png` | 2172 x 724 | Main UnlimitedPDF wordmark |
| `branding/unlimitedpdf-icon.png` | 1254 x 1254 | Standalone application icon |
| `nuget/unlimitedpdf-package-icon.png` | 1254 x 1254 | NuGet package icon artwork |
| `banners/unlimitedpdf-github-banner.png` | 1774 x 887 | Project banner |
| `banners/unlimitedpdf-social-card.png` | 1731 x 909 | Promotional card |
| `banners/unlimitedpdf-demos-hero.png` | 1672 x 941 | Demo website hero artwork (a stylized illustration) |

All originals are below 2 MB and 4096 x 4096 pixels. The three banners have opaque backgrounds.

The demo hero uses the main logo's two-color wordmark: navy **Unlimited** and teal **PDF**, matching `branding/unlimitedpdf-logo.png`. Its trailing **Demos** label stays teal. The manifest records the color correction and its exact image-generation prompt.

The hero's Medical Report Findings bullet was also corrected to "No significant abnormalities detected." The spelling was checked in an enlarged crop before regenerating both responsive JPEGs; the manifest records the correction and prompt.

## Derived copies

`New-DerivedImages.ps1` makes every copy below from the originals and overwrites the previous copies. Run it from Windows PowerShell 5.1 (it uses `System.Drawing`):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File assets/New-DerivedImages.ps1
```

Transparent images have their transparent margins trimmed. Opaque banners are scaled, or cropped where noted. Everything is resized with high-quality bicubic scaling to about twice its largest display size. On the seal, stray, almost fully transparent specks left by background removal are cleared. No other change is made.

Copies as of 2026-11-04 (two runs produce byte-identical files):

| Copy (from the repository root) | Original (in `assets/`) | Size | Bytes | Used by |
|---|---|---|---|---|
| `UnlimitedPDF.Demos.BlazorWasm/wwwroot/images/unlimitedpdf-logo.png` | `branding/unlimitedpdf-logo.png` | 329 x 64 | 17,720 | App top bar wordmark |
| `UnlimitedPDF.Demos.BlazorWasm/wwwroot/images/unlimitedpdf-icon.png` | `branding/unlimitedpdf-icon.png` | 64 x 62 | 4,993 | App top bar logo on phones |
| `UnlimitedPDF.Demos.BlazorWasm/wwwroot/images/favicon.png` | `branding/favicon.png` | 64 x 64 | 4,958 | App browser tab icon |
| `UnlimitedPDF.Demos.BlazorWasm/wwwroot/images/invoice-logo.png` | `originals/invoice-logo.png` | 480 x 86 | 23,662 | Invoice demo |
| `UnlimitedPDF.Demos.BlazorWasm/wwwroot/images/medical-report-logo.png` | `originals/medical-report-logo.png` | 480 x 94 | 29,923 | Medical report demo |
| `UnlimitedPDF.Demos.BlazorWasm/wwwroot/images/receipt-logo.png` | `originals/receipt-logo.png` | 360 x 80 | 13,039 | Receipt demo |
| `UnlimitedPDF.Demos.BlazorWasm/wwwroot/images/certificate-seal.png` | `originals/certificate-seal.png` | 295 x 360 | 125,693 | Certificate demo (specks cleared) |
| `UnlimitedPDF.Demos.BlazorWasm/wwwroot/images/certificate-signature.png` | `originals/certificate-signature.png` | 480 x 121 | 15,744 | Certificate demo (fictional signature) |
| `UnlimitedPDF.Demos.BlazorWasm/wwwroot/images/unlimitedpdf-demos-hero-1200.jpg` | `banners/unlimitedpdf-demos-hero.png` | 1200 x 675, JPEG quality 85 | 124,551 | App home showcase (wide screens) |
| `UnlimitedPDF.Demos.BlazorWasm/wwwroot/images/unlimitedpdf-demos-hero-800.jpg` | `banners/unlimitedpdf-demos-hero.png` | 800 x 450, JPEG quality 85 | 68,417 | App home showcase (phones) |
| `assets/derived/unlimitedpdf-github-banner.jpg` | `banners/unlimitedpdf-github-banner.png` | 1280 x 640, JPEG quality 90 | 120,382 | README.md and nuget/README.md banner |
| `assets/derived/unlimitedpdf-social-preview.jpg` | `banners/unlimitedpdf-social-card.png` | 1280 x 640, JPEG quality 90 | 115,660 | GitHub repository social preview (uploaded in Settings) |
| `nuget/icon.png` | `nuget/unlimitedpdf-package-icon.png` | 128 x 128 | 21,808 | NuGet package icon |
