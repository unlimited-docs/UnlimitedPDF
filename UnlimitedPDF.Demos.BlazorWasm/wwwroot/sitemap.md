# UnlimitedPDF demos site map

Every page of [unlimitedpdf.dev](https://unlimitedpdf.dev/), the live demos of UnlimitedPDF, a C# PDF library for .NET. Each demo builds its PDF in the browser and shows the C# code. Search engines read the same pages from [sitemap.xml](https://unlimitedpdf.dev/sitemap.xml); a summary of the library for agents is in [llms.txt](https://unlimitedpdf.dev/llms.txt).

## Site

- [UnlimitedPDF: C# PDF library for .NET, with live demos](https://unlimitedpdf.dev/): Try UnlimitedPDF, the C# PDF library for .NET, in your browser: create PDFs with text, tables, and images, merge and split PDFs, and convert HTML to PDF.
- [C# PDF examples by scenario](https://unlimitedpdf.dev/playground): Every UnlimitedPDF demo by capability and business domain: invoices, statements, payslips, merge, split, HTML to PDF, and languages, with C# code.
- [UnlimitedPDF FAQ: C# and .NET PDF library questions](https://unlimitedpdf.dev/faq): Answers about the UnlimitedPDF C# PDF library: install, .NET versions, Blazor WebAssembly, fonts, tables, images, merge, split, HTML to PDF, and license.
- [Get started with UnlimitedPDF: install from NuGet](https://unlimitedpdf.dev/getting-started): Install the UnlimitedPDF C# PDF library from NuGet, write your first PDF in three lines, and find the scenario for your task.
- [Free PDF tools: merge, split, convert](https://unlimitedpdf.dev/tools): Merge, split, and convert your own files for free. Built with UnlimitedPDF, running in your browser: nothing is uploaded.

## Create PDFs

- [Add text and pages to a PDF in C#](https://unlimitedpdf.dev/playground/create/text-and-pages): Choose a page size, place text at any position and size, and add pages.
- [Create PDF tables in C#](https://unlimitedpdf.dev/playground/create/tables): Column widths, colors, borders, padding, alignment, and row height rules.
- [Add JPEG and PNG images to a PDF in C#](https://unlimitedpdf.dev/playground/create/images): Place JPEG and PNG images with fit modes, sizes, and inside table cells. Try your own image.
- [PDF headers, footers, and page numbers in C#](https://unlimitedpdf.dev/playground/create/headers-and-footers): A report with a header and footer on every page, page numbers, first and last page settings, and content that flows onto new pages.

## Merge PDFs

- [Merge PDF files in C#](https://unlimitedpdf.dev/playground/merge/join-files): Join whole PDFs in one call, in the order you choose.
- [Merge a cover page and PDF page ranges in C#](https://unlimitedpdf.dev/playground/merge/cover-and-ranges): Put a built cover page in front of chosen page ranges from each file.
- [Extract and reorder PDF pages in C#](https://unlimitedpdf.dev/playground/merge/pick-pages): Take the pages you want from a PDF, in the order you want, such as 3, 1-2.
- [Merged PDF bookmarks and page numbers in C#](https://unlimitedpdf.dev/playground/merge/bookmarks-and-numbers): Name each file's bookmark; the contents and closing pages show page numbers that count every merged page.
- [Generate and merge PDF document packs in C#](https://unlimitedpdf.dev/playground/merge/document-packs): Generate a month-end, employee, or patient pack from the other demos and merge it behind a cover page, with a bookmark per document.

## Split, extract, and remove PDF pages

- [Split a PDF every N pages in C#](https://unlimitedpdf.dev/playground/split/every-n-pages): Split a PDF into parts of the same number of pages, such as one file per page or every 10 pages.
- [Split a PDF by page ranges in C#](https://unlimitedpdf.dev/playground/split/page-ranges): Make one PDF per page range you choose, such as 1-2, 3, 2-4. Ranges may overlap and leave pages out.
- [Extract pages from a PDF in C#](https://unlimitedpdf.dev/playground/split/extract-pages): Copy the pages you choose, in your order, into one new PDF, such as 4, 1-2. A page can appear more than once.
- [Remove pages from a PDF in C#](https://unlimitedpdf.dev/playground/split/remove-pages): Write the PDF without the pages you choose, such as 1-4. The pages left keep their order.

## HTML to PDF

- [Convert HTML to PDF in C#](https://unlimitedpdf.dev/playground/html/html-to-pdf): Pick a template, including an Arabic invoice and a Hebrew letter that read from the right, or paste your own HTML: no engine required, nothing uploaded.
- [Common HTML to PDF problems in C#](https://unlimitedpdf.dev/playground/html/common-problems): Rows cut at page breaks, headers that don't repeat, long URLs, nested tables and lists, and print styles: open a problem, see the PDF, edit the HTML.
- [Table row cut in half: HTML to PDF in C#](https://unlimitedpdf.dev/playground/html/common-problems/row-cut-in-half): At a page break, a table row is cut through its text: the top half of the row is on one page and the bottom half on the next. See the PDF and edit the HTML.
- [Header row not repeating: HTML to PDF in C#](https://unlimitedpdf.dev/playground/html/common-problems/header-not-repeating): A long table's header row is shown on the first page only, so later pages have columns without names. See the PDF and edit the HTML.
- [Row taller than a page: HTML to PDF in C#](https://unlimitedpdf.dev/playground/html/common-problems/row-taller-than-page): A single row taller than a page is clipped at the bottom of the page, or overflows past it. See the PDF and edit the HTML.
- [break-inside: avoid ignored: HTML to PDF](https://unlimitedpdf.dev/playground/html/common-problems/break-inside-avoid): A block marked break-inside: avoid (or page-break-inside: avoid) is still split across two pages. See the PDF and edit the HTML.
- [Forced page breaks ignored: HTML to PDF in C#](https://unlimitedpdf.dev/playground/html/common-problems/forced-page-breaks): page-break-before: always and break-after: page are ignored, so chapters run on after each other. See the PDF and edit the HTML.
- [Line sliced across pages: HTML to PDF in C#](https://unlimitedpdf.dev/playground/html/common-problems/line-sliced): A line of text at the bottom of a page is drawn half on each page, with the bottom of its letters cut off. See the PDF and edit the HTML.
- [Image split across pages: HTML to PDF in C#](https://unlimitedpdf.dev/playground/html/common-problems/image-split): An image near the bottom of a page is cut in two, with its top on one page and its bottom on the next. See the PDF and edit the HTML.
- [Blank page before tall content: HTML to PDF](https://unlimitedpdf.dev/playground/html/common-problems/blank-page-before-tall-content): Content taller than a page is pushed to a new page, leaving an empty page (or a page with only a heading) before it. See the PDF and edit the HTML.
- [Nested tables with spans: HTML to PDF in C#](https://unlimitedpdf.dev/playground/html/common-problems/nested-tables): Tables nested three levels deep, with colspan and rowspan, overlap each other or lose cells. See the PDF and edit the HTML.
- [800 px email table: HTML to PDF in C#](https://unlimitedpdf.dev/playground/html/common-problems/email-table-800px): An HTML email built as a fixed 800 px wide table runs off the right edge of the page and is cut off. See the PDF and edit the HTML.
- [Unbreakable word in a cell: HTML to PDF in C#](https://unlimitedpdf.dev/playground/html/common-problems/long-word-in-cell): A long word or reference with no spaces widens its cell, pushing the table past the right edge of the page. See the PDF and edit the HTML.
- [Cell styles lost at a page break: HTML to PDF](https://unlimitedpdf.dev/playground/html/common-problems/cell-styles-at-break): When a row splits across pages, its cell backgrounds and borders are missing on one of the parts. See the PDF and edit the HTML.
- [Numbering restarts on a new page: HTML to PDF](https://unlimitedpdf.dev/playground/html/common-problems/numbering-across-pages): An ordered list that crosses a page break restarts at 1, or its numbers disappear on the next page. See the PDF and edit the HTML.
- [Six-level mixed lists: HTML to PDF in C#](https://unlimitedpdf.dev/playground/html/common-problems/mixed-nested-lists): Deeply nested lists that mix numbers and bullets, with start, reversed, or value, show the wrong markers. See the PDF and edit the HTML.
- [Lists inside table cells: HTML to PDF in C#](https://unlimitedpdf.dev/playground/html/common-problems/lists-in-cells): Lists inside table cells lose their markers or their indentation. See the PDF and edit the HTML.
- [Long URL runs off the page: HTML to PDF in C#](https://unlimitedpdf.dev/playground/html/common-problems/long-url): A long URL has no spaces to wrap at, so it runs past the right edge of the page and is cut off. See the PDF and edit the HTML.
- [Preformatted text wider than the page](https://unlimitedpdf.dev/playground/html/common-problems/wide-pre): Code or log lines in <pre> are wider than the page and are cut off at the right edge. See the PDF and edit the HTML.
- [Characters shown as boxes: HTML to PDF in C#](https://unlimitedpdf.dev/playground/html/common-problems/non-latin-characters): Accented letters, currency signs, or other scripts show as empty boxes or missing characters. See the PDF and edit the HTML.
- [Non-breaking spaces and whitespace](https://unlimitedpdf.dev/playground/html/common-problems/nbsp-and-whitespace): Amounts joined with &nbsp; still break across lines, or runs of spaces and line breaks in the source show up as gaps. See the PDF and edit the HTML.
- [Relative and data: images missing](https://unlimitedpdf.dev/playground/html/common-problems/relative-and-data-images): Images named by a relative path or written as data: URIs come out blank. See the PDF and edit the HTML.
- [Image wider than the page: HTML to PDF in C#](https://unlimitedpdf.dev/playground/html/common-problems/image-wider-than-page): A large image is drawn at its full size and runs off the right edge of the page. See the PDF and edit the HTML.
- [SVG and WebP images: HTML to PDF in C#](https://unlimitedpdf.dev/playground/html/common-problems/svg-and-webp): SVG and WebP images are missing from the PDF, or the conversion fails. See the PDF and edit the HTML.
- [Links inside the document: HTML to PDF in C#](https://unlimitedpdf.dev/playground/html/common-problems/anchor-links): Links to a part of the same document (#id) do nothing in the PDF. See the PDF and edit the HTML.
- [External links: HTML to PDF in C#](https://unlimitedpdf.dev/playground/html/common-problems/external-links): Links are not clickable in the PDF, or javascript: links are kept. See the PDF and edit the HTML.
- [Print styles ignored: HTML to PDF in C#](https://unlimitedpdf.dev/playground/html/common-problems/print-media): @media print rules are ignored, or screen-only parts such as menus end up in the PDF. See the PDF and edit the HTML.
- [Flexbox and grid layouts: HTML to PDF in C#](https://unlimitedpdf.dev/playground/html/common-problems/flex-and-grid): Pages built with flexbox or grid collapse, overlap, or lose their columns. See the PDF and edit the HTML.
- [Background colors: HTML to PDF in C#](https://unlimitedpdf.dev/playground/html/common-problems/backgrounds): Background colors are missing, or they are drawn over the text and hide it. See the PDF and edit the HTML.
- [Unclosed and misnested tags: HTML to PDF](https://unlimitedpdf.dev/playground/html/common-problems/malformed-html): Sloppy HTML with unclosed or misnested tags loses text or breaks the conversion. See the PDF and edit the HTML.

## Languages

- [Multilingual PDFs with your own fonts in C#](https://unlimitedpdf.dev/playground/languages/fonts-and-languages): An invoice in 24 languages, from Hindi and Tamil to Arabic, Urdu, Hebrew, Greek, and Russian, or a mix, drawn with included free fonts or your own.
- [Arabic and Hebrew right-to-left PDFs in C#](https://unlimitedpdf.dev/playground/languages/right-to-left-documents): Invoices in Arabic, Persian, Urdu, Hebrew, and Dhivehi that read from the right: switch the direction, draw with text calls or HTML, and copy the C# code.

## Complete business documents

- [Create a PDF invoice in C#](https://unlimitedpdf.dev/playground/documents/invoice): A tax invoice with a logo, product photos in the line items, and computed totals.
- [Create a PDF quotation in C#](https://unlimitedpdf.dev/playground/documents/invoice?type=quotation): A services quotation with its own first-page header, a last-page footer, and terms kept with the signature.
- [Create a PDF purchase order in C#](https://unlimitedpdf.dev/playground/documents/invoice?type=purchase-order): A purchase order whose item table runs across pages, with the totals kept together at the end.
- [Create a PDF delivery note in C#](https://unlimitedpdf.dev/playground/documents/invoice?type=delivery-note): A delivery note that lists ordered and shipped quantities and keeps the signature block together.
- [Create a PDF receipt in C#](https://unlimitedpdf.dev/playground/documents/invoice?type=receipt): A compact A6 store receipt with items, tax, and payment details.
- [Generate invoices in bulk in C#: 1,000 PDFs](https://unlimitedpdf.dev/playground/documents/bulk-invoices): Make up to 1,000 invoices in your browser, see the total time and the time per invoice, and download them all as one zip file.
- [Create a PDF bank statement in C#](https://unlimitedpdf.dev/playground/documents/account-statement): A bank statement with a first-page summary, a running balance, and the header row on every page.
- [Create a grouped PDF inventory report in C#](https://unlimitedpdf.dev/playground/documents/inventory-report): Stock grouped by warehouse, with a subtotal per warehouse and the header row repeated on every page.
- [Create a PDF payslip in C#](https://unlimitedpdf.dev/playground/documents/payslip): A one-page payslip with earnings, deductions, and net pay, held to one page by a page limit.
- [Create a PDF contract from HTML in C#](https://unlimitedpdf.dev/playground/documents/contract): An employment agreement written in HTML: numbered clauses, long paragraphs that wrap across pages, and page X of Y.
- [Create a PDF medical report in C#](https://unlimitedpdf.dev/playground/documents/medical-report): A laboratory report with patient details, results, reference ranges, and flags.
- [Create a PDF KPI report with bar charts in C#](https://unlimitedpdf.dev/playground/documents/kpi-report): Headline figures and a bar chart of monthly revenue, drawn from rectangles on one page.
- [Print PDF shipping labels on A4 sheets in C#](https://unlimitedpdf.dev/playground/documents/shipping-labels): Address labels on A4 sheets of three by seven, outlined for cutting and ready to print.

## Free PDF tools

- [Merge PDF files free, in your browser](https://unlimitedpdf.dev/tools/merge): Join several PDFs into one, in the order you choose, right in your browser.
- [Convert JPG and PNG images to PDF free, in your browser](https://unlimitedpdf.dev/tools/images-to-pdf): Turn JPG and PNG pictures into one PDF, one picture per page.
- [Split a PDF into several files free, in your browser](https://unlimitedpdf.dev/tools/split): Cut one PDF into several files, every few pages or at the page ranges you type.
- [Extract pages from a PDF free, in your browser](https://unlimitedpdf.dev/tools/extract-pages): Get a new PDF with only the pages you pick, right in your browser.
- [Remove pages from a PDF free, in your browser](https://unlimitedpdf.dev/tools/remove-pages): Delete the pages you don't need and keep the rest, in order, right in your browser.
- [Convert HTML to PDF free, in your browser](https://unlimitedpdf.dev/tools/html-to-pdf): Paste HTML or choose an .html file, add the pictures it uses, and get a PDF.
