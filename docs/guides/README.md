# UnlimitedPDF guides

[README](../../README.md) · [Changelog](../../CHANGELOG.md) · [Live demos](https://unlimitedpdf.dev/)

Each guide shows the code for one capability, the PDF it writes, and the rules and limits to know.

| Guide | What it covers | Live demo |
|---|---|---|
| [Create PDFs](create-pdfs.md) | Text, pages, and tables, with a full invoice | [Text and pages](https://unlimitedpdf.dev/playground/create/text-and-pages), [Tables](https://unlimitedpdf.dev/playground/create/tables) |
| [Add images](images.md) | JPEG and PNG, fit modes, units, images in table cells | [Images](https://unlimitedpdf.dev/playground/create/images) |
| [Tables across pages](tables-across-pages.md) | Long tables that continue on new pages with repeated header rows | [Tables](https://unlimitedpdf.dev/playground/create/tables) |
| [Headers, footers & flow](headers-footers-and-flow.md) | Headers, footers, page numbers, and content that flows from page to page | [Headers and footers](https://unlimitedpdf.dev/playground/create/headers-and-footers) |
| [Merge PDFs](merge-pdfs.md) | Join files, add page ranges after a cover page, keep bookmarks | [Join files](https://unlimitedpdf.dev/playground/merge/join-files) |
| [Split PDFs](split-pdfs.md) | Every N pages or by page range, to a folder or to streams | [Every N pages](https://unlimitedpdf.dev/playground/split/every-n-pages) |
| [Extract and remove pages](extract-and-remove-pages.md) | The pages you choose, in your order, or a PDF without them | [Extract pages](https://unlimitedpdf.dev/playground/split/extract-pages) |
| [HTML to PDF](html-to-pdf.md) | HTML and CSS to PDF with the built-in renderer, no browser | [HTML to PDF](https://unlimitedpdf.dev/playground/html/html-to-pdf) |
| [Fonts and languages](fonts-and-languages.md) | Your own fonts, 17 scripts, right-to-left documents | [Fonts and languages](https://unlimitedpdf.dev/playground/languages/fonts-and-languages) |
| [Upgrading to 1.0.0](upgrading-to-1.0.0.md) | Old and new names for code written for 0.0.x | |

## Demos

`UnlimitedPDF.Demos.BlazorWasm` is an interactive site that runs entirely in the browser: no engine required, nothing uploaded. Its scenario gallery lists 26 scenarios by capability and business domain, and each scenario shows your data, the PDF drawn in the page, and the C# code side by side. Create: text and pages, tables, images, and headers and footers. Merge: join files, cover and ranges, pick pages, bookmarks and numbers, and document packs for finance, HR, and healthcare. Split and extract: every N pages, page ranges, extract pages, and remove pages. HTML to PDF: templates, including an Arabic invoice and a Hebrew letter that read from the right, or your own HTML, and Common problems: HTML that often goes wrong in PDF, which you can open and edit. Languages (Fonts and languages, with text calls or HTML): an invoice in Hindi, Bengali, Gujarati, Punjabi, Odia, Kannada, Malayalam, Telugu, Tamil, French, Polish, Czech, Turkish, Filipino, Greek, Russian, Ukrainian, Armenian, Georgian, Arabic, Persian, Urdu, Hebrew, Dhivehi, or a mix, drawn with free fonts included for the demo or fonts you upload; and Right-to-left documents: the Arabic, Persian, Urdu, Hebrew, and Dhivehi invoices with a direction switch (right to left, Auto, or left to right) to see what the setting changes. Real-life documents: invoice (also quotation, purchase order, delivery note, and receipt), bulk invoices (up to 1,000, zipped), account statement (up to 5,000 rows), inventory report, payslip, contract, medical report, KPI report, and shipping labels. Each scenario has editable data, a download, and the C# code that builds it.

Try it live at [unlimitedpdf.dev](https://unlimitedpdf.dev/), or run it locally:

```bash
dotnet run --project UnlimitedPDF.Demos.BlazorWasm
```
