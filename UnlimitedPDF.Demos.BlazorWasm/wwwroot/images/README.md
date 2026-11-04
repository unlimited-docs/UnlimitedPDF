# Demo app images

Images used by the UnlimitedPDF demo app, released under Apache-2.0 with the rest of the repository (PR-008, PR-011). The app embeds these files in the PDFs it generates, and shows some of them in its own pages.

## Reused from the console demo

Copied unchanged from [`UnlimitedPDF.Demo.ConsoleApp/Assets`](../../../UnlimitedPDF.Demo.ConsoleApp/Assets/README.md). They were generated on 2026-11-01 by `Generate-DemoAssets.ps1`, which draws simple shapes and uses no fonts or third-party artwork.

| File | Size | Format | Used by |
|---|---|---|---|
| `keyboard.jpg` | 96 x 64 px | Baseline RGB JPEG | Invoice line items, Images demo |
| `dock.jpg` | 96 x 64 px | Baseline RGB JPEG | Invoice line items, Images demo |
| `cmyk-sample.jpg` | 128 x 96 px | Baseline CMYK JPEG with Adobe segment | Images demo |

## Generated UnlimitedPDF images

The project owner created the artwork with ChatGPT image generation on 2026-11-04. The full-size originals, their checksums, and their origin are in the repository's [`assets/`](../../../assets/ARTWORK.md) folder, which is neither served nor published (PR-013).

The files here are size-optimized copies made by [`assets/New-DerivedImages.ps1`](../../../assets/New-DerivedImages.ps1). Run that script to regenerate them; don't edit them by hand. Each copy and its original are listed in [`assets/ARTWORK.md`](../../../assets/ARTWORK.md#derived-copies).

| File | Used by |
|---|---|
| `unlimitedpdf-logo.png` | Top bar wordmark |
| `unlimitedpdf-icon.png` | Top bar logo on phones |
| `favicon.png` | Browser tab icon |
| `unlimitedpdf-demos-hero-1200.jpg`, `unlimitedpdf-demos-hero-800.jpg` | Home page showcase (a stylized illustration) |
| `invoice-logo.png` | Invoice demo |
| `medical-report-logo.png` | Medical report demo |
| `receipt-logo.png` | Receipt demo |
| `certificate-seal.png`, `certificate-signature.png` | Certificate demo (the signature is fictional) |
