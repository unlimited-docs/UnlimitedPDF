# UnlimitedPDF artwork (originals)

Released under Apache-2.0 with the rest of the repository (PR-008, PR-011).

Created with ChatGPT image generation for the project owner and downloaded on 2026-11-04 from the [shared asset chat](https://chatgpt.com/share/6aeb010e-7d90-83e8-b31b-ae43794edd92). This folder contains 12 PNG artwork files from that chat, plus the five demo logos described under [Demo logos added 2026-11-06](#demo-logos-added-2026-11-06). On 2026-11-04, the GitHub banner and social card were edited with the built-in image generation tool to replace unsupported API examples in their code windows. Their surrounding designs and original dimensions were retained. The demo hero was also edited to use the current UnlimitedPDF Demos logo once, replacing the stacked logo and duplicate headline while retaining the subtitle and four document illustrations. Its 800- and 1200-pixel responsive copies were regenerated with `New-DerivedImages.ps1`. The remaining 9 files are unchanged originals. The separate download for the resized seven-file demo pack was unavailable, so original dimensions and sizes differ from those advertised for that ZIP.

These are source files. They live here, outside every served or packed folder, so no published site or package carries them (PR-013). The demo app, the package, and the readmes use the smaller copies listed under [Derived copies](#derived-copies). `generated-assets-manifest.json` records dimensions, sizes, source asset identifiers, and SHA-256 checksums. The two corrected banners also have edit records containing the exact generation prompts and displayed C# snippets. Both snippets were compiled and executed against the local `UnlimitedPDF.csproj` with .NET SDK 10.0.403, and their rendered text was visually checked. They use `AddPage()`, `AddText(...)`, and `Save(...)` as implemented in `PdfDocument.cs` and `PdfPageBuilder.cs`.

| File | Dimensions | Purpose |
|---|---|---|
| `originals/invoice-logo.png` | 2172 x 724 | Invoice demo logo |
| `originals/medical-report-logo.png` | 2172 x 724 | Medical report demo logo |
| `originals/receipt-logo.png` | 1983 x 793 | Receipt demo logo |
| `originals/purchase-order-logo.png` | 2172 x 724 | Purchase order demo logo (fictional Brightline Manufacturing) |
| `originals/quotation-logo.png` | 2172 x 724 | Quotation demo logo (fictional Summit IT Services) |
| `originals/account-statement-logo.png` | 2172 x 724 | Account statement demo logo (fictional Harborview Savings Bank) |
| `originals/payslip-logo.png` | 2172 x 724 | Payslip demo logo (fictional Greenfield Logistics) |
| `originals/delivery-note-logo.png` | 2172 x 724 | Delivery note demo logo (fictional Meridian Freight) |
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

## Demo logos added 2026-11-06

The five business-document demo logos (Purchase order, Quotation, Account statement, Payslip, Delivery note) are for fictional companies. Created by the project owner with Codex image generation on 2026-11-06 from the prompt recorded here (26110501-page-header-body-footer D-028). They are released under Apache-2.0 with the rest of the repository (PR-008, PR-011). Their dimensions, sizes, and SHA-256 checksums, with each exact prompt, are in `generated-assets-manifest.json`. Each original was checked on 2026-11-06 for spelling, a transparent background, and its look on a white page at the served size. The copies clear their faint background-removal specks (`specksMinArea` 400, as for the seal).

| Original | Company |
|---|---|
| `originals/purchase-order-logo.png` | Brightline Manufacturing |
| `originals/quotation-logo.png` | Summit IT Services |
| `originals/account-statement-logo.png` | Harborview Savings Bank |
| `originals/payslip-logo.png` | Greenfield Logistics |
| `originals/delivery-note-logo.png` | Meridian Freight |

## Document gallery pictures added 2026-11-11

The ten pictures of the home page gallery (26111101-home-page-redesign AC-07) are not generated artwork. Each is the first page of the PDF that its playground demo generates with its default input, as the browser's built-in PDF viewer draws it, captured on 2026-11-11. The PDFs were built by each demo's own sample builder through `DocumentGallery` with the app's served images, so they are the documents the demo pages produce that day. Each page was shown at fit to width at 1.5 device pixels per CSS pixel and cropped to the page: 1479 pixels wide for a one-page document, 1457 for the two that have more pages (the viewer's scroll bar takes the difference), and 1460 for the A6 receipt. The pictures show that day's dates; a later date alone does not make them out of date.

Released under Apache-2.0 with the rest of the repository (PR-011). Their dimensions, sizes, and SHA-256 checksums are in `generated-assets-manifest.json`. `UnlimitedPDF.Tests/Fixtures/document-gallery.json` records what each was captured from: its sample file and every input file the sample uses, and the served images it draws (from `DocumentGallery`). Its SHA-256 covers each source's path and text with LF line endings, then each image's path and bytes. The home page tests fail and name the document when one of them changes, or when the list misses a file in `Samples/Inputs` named after a type the sample, or one of those files, uses; its picture is then captured again the same way. A helper elsewhere in `Samples/` or a type in a file of another name is not followed, so a sample that starts to use one needs that file added to its list by hand.

| Original | Size | Demo |
|---|---|---|
| `originals/gallery/invoice.png` | 1479 x 2092 | Invoice (`playground/documents/invoice`) |
| `originals/gallery/quotation.png` | 1479 x 2092 | Quotation (`playground/documents/quotation`) |
| `originals/gallery/purchase-order.png` | 1479 x 2092 | Purchase order (`playground/documents/purchase-order`) |
| `originals/gallery/delivery-note.png` | 1479 x 2092 | Delivery note (`playground/documents/delivery-note`) |
| `originals/gallery/account-statement.png` | 1457 x 2061 | Account statement (`playground/documents/account-statement`) |
| `originals/gallery/payslip.png` | 1479 x 2092 | Payslip (`playground/documents/payslip`) |
| `originals/gallery/medical-report.png` | 1479 x 2092 | Medical report (`playground/documents/medical-report`) |
| `originals/gallery/receipt.png` | 1460 x 2058 | Receipt (`playground/documents/receipt`) |
| `originals/gallery/certificate.png` | 1479 x 2092 | Certificate (`playground/documents/certificate`) |
| `originals/gallery/employee-directory.png` | 1457 x 2061 | Employee directory (`playground/documents/employee-directory`) |

On 2026-11-11 the copy script was run on a machine whose imaging library writes the earlier copies with different bytes, so those copies were left as committed and only the twenty gallery copies below were added. Two runs there produce byte-identical gallery copies.

## Readme screenshots added 2026-11-12

The eight screenshots in `README.md` and `nuget/README.md` (QF-26111208) are not generated artwork. Each shows the PDF that a readme example writes when it runs as written, as the browser's built-in PDF viewer draws it at fit to width at 1.5 device pixels per CSS pixel, captured on 2026-11-12. Each page was cropped from the viewer and laid on a light gray background with a thin border; pictures with more than one page have a short caption under each. Only the inputs the examples read from disk were supplied:

- Add images: `logo.png` is the demo app's `images/invoice-logo.png`, and the uploaded picture is `images/unlimitedpdf-demos-hero-800.jpg`.
- Merge PDFs: `report.pdf` is the "Headers, footers & flow" example's output, and `invoice.pdf` is the "Create a sample invoice" example's output.
- Split PDFs: `report.pdf` is the "Headers, footers & flow" example with 330 rows instead of 120, so it has the 10 pages the page-range call needs. The picture shows the `chapters` parts.
- HTML to PDF: `invoice.html` is the demo app's invoice template (`Samples/HtmlTemplates.cs`), with `images/invoice-logo.png` as its `logo.png`.

Sample and invoice show the top of their page only (30% and 56%), where all their content is. The pictures show that day's dates; a later date alone does not make them out of date. Released under Apache-2.0 with the rest of the repository (PR-011). Their dimensions, sizes, and SHA-256 checksums are in `generated-assets-manifest.json`. As on 2026-11-11, only the eight readme copies below were added; the earlier copies were left as committed.

| Original | Size | Readme section |
|---|---|---|
| `originals/readme/sample.png` | 1172 x 540 | Sample code |
| `originals/readme/invoice.png` | 1172 x 945 | Create a sample invoice |
| `originals/readme/images.png` | 832 x 1148 | Add images |
| `originals/readme/tables-across-pages.png` | 1224 x 909 | Tables across pages (pages 1 and 2) |
| `originals/readme/headers-footers.png` | 1336 x 683 | Headers, footers & flow (pages 1, 2, and 4) |
| `originals/readme/merge.png` | 1448 x 569 | Merge PDFs (`pack.pdf`) |
| `originals/readme/split.png` | 1672 x 456 | Split PDFs (`chapters/report-1.pdf` and `report-2.pdf`) |
| `originals/readme/html-to-pdf.png` | 832 x 1148 | HTML to PDF (`invoice.pdf`) |

## Derived copies

`New-DerivedImages.ps1` makes every copy below from the originals and overwrites the previous copies. Run it from Windows PowerShell 5.1 (it uses `System.Drawing`):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File assets/New-DerivedImages.ps1
```

Transparent images have their transparent margins trimmed. Opaque banners are scaled, or cropped where noted. Everything is resized with high-quality bicubic scaling to about twice its largest display size. On the seal, stray, almost fully transparent specks left by background removal are cleared. No other change is made.

Copies as of 2026-11-06 (two runs produce byte-identical files; the 2026-11-04 copies are unchanged):

| Copy (from the repository root) | Original (in `assets/`) | Size | Bytes | Used by |
|---|---|---|---|---|
| `UnlimitedPDF.Demos.BlazorWasm/wwwroot/images/unlimitedpdf-logo.png` | `branding/unlimitedpdf-logo.png` | 329 x 64 | 17,720 | App top bar wordmark |
| `UnlimitedPDF.Demos.BlazorWasm/wwwroot/images/unlimitedpdf-icon.png` | `branding/unlimitedpdf-icon.png` | 64 x 62 | 4,993 | App top bar logo on phones |
| `UnlimitedPDF.Demos.BlazorWasm/wwwroot/images/favicon.png` | `branding/favicon.png` | 64 x 64 | 4,958 | App browser tab icon |
| `UnlimitedPDF.Demos.BlazorWasm/wwwroot/images/invoice-logo.png` | `originals/invoice-logo.png` | 480 x 86 | 23,662 | Invoice demo |
| `UnlimitedPDF.Demos.BlazorWasm/wwwroot/images/medical-report-logo.png` | `originals/medical-report-logo.png` | 480 x 94 | 29,923 | Medical report demo |
| `UnlimitedPDF.Demos.BlazorWasm/wwwroot/images/receipt-logo.png` | `originals/receipt-logo.png` | 360 x 80 | 13,039 | Receipt demo |
| `UnlimitedPDF.Demos.BlazorWasm/wwwroot/images/purchase-order-logo.png` | `originals/purchase-order-logo.png` | 442 x 120 | 44,105 | Purchase order demo (specks cleared) |
| `UnlimitedPDF.Demos.BlazorWasm/wwwroot/images/quotation-logo.png` | `originals/quotation-logo.png` | 399 x 120 | 39,178 | Quotation demo (specks cleared) |
| `UnlimitedPDF.Demos.BlazorWasm/wwwroot/images/account-statement-logo.png` | `originals/account-statement-logo.png` | 415 x 120 | 39,054 | Account statement demo (specks cleared) |
| `UnlimitedPDF.Demos.BlazorWasm/wwwroot/images/payslip-logo.png` | `originals/payslip-logo.png` | 443 x 120 | 43,746 | Payslip demo (specks cleared) |
| `UnlimitedPDF.Demos.BlazorWasm/wwwroot/images/delivery-note-logo.png` | `originals/delivery-note-logo.png` | 352 x 120 | 34,429 | Delivery note demo (specks cleared) |
| `UnlimitedPDF.Demos.BlazorWasm/wwwroot/images/certificate-seal.png` | `originals/certificate-seal.png` | 295 x 360 | 125,693 | Certificate demo (specks cleared) |
| `UnlimitedPDF.Demos.BlazorWasm/wwwroot/images/certificate-signature.png` | `originals/certificate-signature.png` | 480 x 121 | 15,744 | Certificate demo (fictional signature) |
| `UnlimitedPDF.Demos.BlazorWasm/wwwroot/images/unlimitedpdf-demos-hero-1200.jpg` | `banners/unlimitedpdf-demos-hero.png` | 1200 x 675, JPEG quality 85 | 124,551 | App home showcase (wide screens) |
| `UnlimitedPDF.Demos.BlazorWasm/wwwroot/images/unlimitedpdf-demos-hero-800.jpg` | `banners/unlimitedpdf-demos-hero.png` | 800 x 450, JPEG quality 85 | 68,417 | App home showcase (phones) |
| `assets/derived/unlimitedpdf-github-banner.jpg` | `banners/unlimitedpdf-github-banner.png` | 1280 x 640, JPEG quality 90 | 120,382 | README.md and nuget/README.md banner |
| `assets/derived/unlimitedpdf-social-preview.jpg` | `banners/unlimitedpdf-social-card.png` | 1280 x 640, JPEG quality 90 | 115,660 | GitHub repository social preview (uploaded in Settings) |
| `nuget/icon.png` | `nuget/unlimitedpdf-package-icon.png` | 128 x 128 | 21,808 | NuGet package icon |
| `UnlimitedPDF.Demos.BlazorWasm/wwwroot/images/gallery/invoice-320.jpg` | `originals/gallery/invoice.png` | 320 x 453, JPEG quality 90 | 21,566 | App home gallery tile |
| `UnlimitedPDF.Demos.BlazorWasm/wwwroot/images/gallery/invoice-960.jpg` | `originals/gallery/invoice.png` | 960 x 1358, JPEG quality 90 | 114,845 | App home quick look |
| `UnlimitedPDF.Demos.BlazorWasm/wwwroot/images/gallery/quotation-320.jpg` | `originals/gallery/quotation.png` | 320 x 453, JPEG quality 90 | 20,312 | App home gallery tile |
| `UnlimitedPDF.Demos.BlazorWasm/wwwroot/images/gallery/quotation-960.jpg` | `originals/gallery/quotation.png` | 960 x 1358, JPEG quality 90 | 117,977 | App home quick look |
| `UnlimitedPDF.Demos.BlazorWasm/wwwroot/images/gallery/purchase-order-320.jpg` | `originals/gallery/purchase-order.png` | 320 x 453, JPEG quality 90 | 21,115 | App home gallery tile |
| `UnlimitedPDF.Demos.BlazorWasm/wwwroot/images/gallery/purchase-order-960.jpg` | `originals/gallery/purchase-order.png` | 960 x 1358, JPEG quality 90 | 117,939 | App home quick look |
| `UnlimitedPDF.Demos.BlazorWasm/wwwroot/images/gallery/delivery-note-320.jpg` | `originals/gallery/delivery-note.png` | 320 x 453, JPEG quality 90 | 18,985 | App home gallery tile |
| `UnlimitedPDF.Demos.BlazorWasm/wwwroot/images/gallery/delivery-note-960.jpg` | `originals/gallery/delivery-note.png` | 960 x 1358, JPEG quality 90 | 101,788 | App home quick look |
| `UnlimitedPDF.Demos.BlazorWasm/wwwroot/images/gallery/account-statement-320.jpg` | `originals/gallery/account-statement.png` | 320 x 453, JPEG quality 80 | 37,471 | App home gallery tile |
| `UnlimitedPDF.Demos.BlazorWasm/wwwroot/images/gallery/account-statement-960.jpg` | `originals/gallery/account-statement.png` | 960 x 1358, JPEG quality 45 | 146,543 | App home quick look |
| `UnlimitedPDF.Demos.BlazorWasm/wwwroot/images/gallery/payslip-320.jpg` | `originals/gallery/payslip.png` | 320 x 453, JPEG quality 90 | 21,329 | App home gallery tile |
| `UnlimitedPDF.Demos.BlazorWasm/wwwroot/images/gallery/payslip-960.jpg` | `originals/gallery/payslip.png` | 960 x 1358, JPEG quality 90 | 119,957 | App home quick look |
| `UnlimitedPDF.Demos.BlazorWasm/wwwroot/images/gallery/medical-report-320.jpg` | `originals/gallery/medical-report.png` | 320 x 453, JPEG quality 90 | 22,380 | App home gallery tile |
| `UnlimitedPDF.Demos.BlazorWasm/wwwroot/images/gallery/medical-report-960.jpg` | `originals/gallery/medical-report.png` | 960 x 1358, JPEG quality 90 | 123,626 | App home quick look |
| `UnlimitedPDF.Demos.BlazorWasm/wwwroot/images/gallery/receipt-320.jpg` | `originals/gallery/receipt.png` | 320 x 451, JPEG quality 90 | 29,084 | App home gallery tile |
| `UnlimitedPDF.Demos.BlazorWasm/wwwroot/images/gallery/receipt-960.jpg` | `originals/gallery/receipt.png` | 960 x 1353, JPEG quality 90 | 134,964 | App home quick look |
| `UnlimitedPDF.Demos.BlazorWasm/wwwroot/images/gallery/certificate-320.jpg` | `originals/gallery/certificate.png` | 320 x 453, JPEG quality 90 | 21,389 | App home gallery tile |
| `UnlimitedPDF.Demos.BlazorWasm/wwwroot/images/gallery/certificate-960.jpg` | `originals/gallery/certificate.png` | 960 x 1358, JPEG quality 90 | 111,324 | App home quick look |
| `UnlimitedPDF.Demos.BlazorWasm/wwwroot/images/gallery/employee-directory-320.jpg` | `originals/gallery/employee-directory.png` | 320 x 453, JPEG quality 80 | 36,425 | App home gallery tile |
| `UnlimitedPDF.Demos.BlazorWasm/wwwroot/images/gallery/employee-directory-960.jpg` | `originals/gallery/employee-directory.png` | 960 x 1358, JPEG quality 45 | 148,902 | App home quick look |
| `assets/derived/readme/sample.jpg` | `originals/readme/sample.png` | 1172 x 540, JPEG quality 90 | 40,333 | README.md and nuget/README.md screenshot |
| `assets/derived/readme/invoice.jpg` | `originals/readme/invoice.png` | 1172 x 945, JPEG quality 90 | 114,508 | README.md and nuget/README.md screenshot |
| `assets/derived/readme/images.jpg` | `originals/readme/images.png` | 832 x 1148, JPEG quality 90 | 50,537 | README.md and nuget/README.md screenshot |
| `assets/derived/readme/tables-across-pages.jpg` | `originals/readme/tables-across-pages.png` | 1224 x 909, JPEG quality 90 | 215,240 | README.md and nuget/README.md screenshot |
| `assets/derived/readme/headers-footers.jpg` | `originals/readme/headers-footers.png` | 1336 x 683, JPEG quality 90 | 168,527 | README.md and nuget/README.md screenshot |
| `assets/derived/readme/merge.jpg` | `originals/readme/merge.png` | 1448 x 569, JPEG quality 90 | 111,547 | README.md and nuget/README.md screenshot |
| `assets/derived/readme/split.jpg` | `originals/readme/split.png` | 1672 x 456, JPEG quality 90 | 157,268 | README.md and nuget/README.md screenshot |
| `assets/derived/readme/html-to-pdf.jpg` | `originals/readme/html-to-pdf.png` | 832 x 1148, JPEG quality 90 | 77,154 | README.md and nuget/README.md screenshot |
