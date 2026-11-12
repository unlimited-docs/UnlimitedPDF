<p align="center">
  <img src="assets/derived/unlimitedpdf-github-banner.jpg" alt="UnlimitedPDF: a lightweight .NET library for programmatically creating PDF files" />
</p>

<h3 align="center">UnlimitedPDF Library</h3>

<p align="center">
UnlimitedPDF is an enterprise-grade, pure .NET PDF library for creating PDF documents with text, tables, and JPEG and PNG images, saved to a file or a stream.
</p>

## Status

[![NuGet](https://img.shields.io/nuget/vpre/UnlimitedPDF)](https://www.nuget.org/packages/unlimitedpdf/absoluteLatest)
[![NuGet](https://img.shields.io/nuget/dt/UnlimitedPDF.svg)](https://www.nuget.org/packages/unlimitedpdf/absoluteLatest)

## Table of contents

### [Install](#install)

- **.NET CLI**
  
  Install with [NuGet](https://www.nuget.org/): `dotnet add package UnlimitedPDF --version 1.0.0`
  
- **Package Reference**
  
  Add package reference to project: `<PackageReference Include="UnlimitedPDF" Version="1.0.0" />`

- **Target frameworks**

  One package targets .NET 6, .NET 8, .NET 9, .NET 10, and .NET 11 (`net6.0`, `net8.0`, `net9.0`, `net10.0`, `net11.0`), including Blazor WebAssembly.

## Demos

`UnlimitedPDF.Demos.BlazorWasm` is an interactive playground that runs entirely in the browser. It generates PDFs for text and pages, tables, images, and headers and footers; merges PDFs, including your own uploads, in a Merge category (join files, cover and page ranges, pick pages, bookmarks and page numbers, and month-end, employee, and patient packs); splits PDFs, including your own uploads, in a Split category (every N pages and page ranges, with each part or a zip of all parts to download); converts HTML to PDF in an HTML to PDF category (templates or your own HTML, and Common problems: HTML that often goes wrong in PDF, which you can open and edit); and builds complete business documents: invoice, quotation, purchase order, delivery note, account statement, payslip, medical report, receipt, certificate, and employee directory. Each demo has editable options, a live preview, a download, and the C# code that builds it.

Run it locally:

```bash
dotnet run --project UnlimitedPDF.Demos.BlazorWasm
```

## Sample Code

```cs
using UnlimitedPDF;

var pdfFilePath = @"D:\\unlimited-pdf-sample.pdf";

var pdfDocument = new PdfDocument();

// Add the first page and add content to it.
var page1 = pdfDocument.AddPage();
page1.AddText("UnlimitedPDF library - Sample PDF", 50, 750, 24);
page1.AddText("This is the first line on the first page.", 50, 730, 12);
page1.AddText("This is the second line on the first page.", 50, 710, 12);

pdfDocument.Save(pdfFilePath);
```

### Screenshot

<img width="931" height="829" alt="image" src="https://github.com/user-attachments/assets/c8e0f04d-b0e3-4046-bb25-e9bcc8f316aa" />

## Create a sample invoice

```cs
using UnlimitedPDF;

Console.WriteLine("Start: Unlimited PDF");

var pdfFilePath = @"D:\\unlimited-pdf-sample.pdf";

// Use a 'using' statement for proper resource management with IDisposable
var pdfDocument = new PdfDocument();

// --- Page 1: Invoice ---
var page1 = pdfDocument.AddPage();

// 1. Invoice Header
page1.AddText("INVOICE", 250, 800, 20);

// Seller and Buyer Details
page1.AddText("Seller:", 50, 760, 10);
page1.AddText("ABC Electronics Pvt. Ltd.", 50, 748, 10);
page1.AddText("123 Tech Park, Bangalore, KA 560001", 50, 736, 10);
page1.AddText("GSTIN: 222222222222225", 50, 724, 10);

page1.AddText("Buyer:", 350, 760, 10);
page1.AddText("UnlimitedPDF", 350, 748, 10);
page1.AddText("456 Code Street, Hyderabad, TS 500001", 350, 736, 10);

// Invoice Metadata
page1.AddText($"Invoice No: INV-{DateTime.Now:yyyy-MM-dd}-001", 50, 690, 10);
page1.AddText($"Date: {DateTime.Now:dd-MMM-yyyy}", 50, 678, 10);

// 2. Items Table
var itemsTable = page1.AddTable(50, 650, 512);
itemsTable.SetColumnWidths(40, 210, 60, 100, 102);

// Table Headers
var headerRow = itemsTable.AddRow();
headerRow.GetCell(0).Text = "S.No.";
headerRow.GetCell(0).BackgroundColor = PdfColor.LightGray;
headerRow.GetCell(1).Text = "Item Description";
headerRow.GetCell(1).BackgroundColor = PdfColor.LightGray;
headerRow.GetCell(2).Text = "Qty";
headerRow.GetCell(2).BackgroundColor = PdfColor.LightGray;
headerRow.GetCell(3).Text = "Rate (INR)";
headerRow.GetCell(3).BackgroundColor = PdfColor.LightGray;
headerRow.GetCell(4).Text = "Amount (INR)";
headerRow.GetCell(4).BackgroundColor = PdfColor.LightGray;

// Item 1
var item1Row = itemsTable.AddRow();
item1Row.GetCell(0).Text = "1";
item1Row.GetCell(1).Text = "Wireless Keyboard";
item1Row.GetCell(2).Text = "2";
item1Row.GetCell(2).HorizontalAlignment = PdfCellHorizontalAlignment.Right;
item1Row.GetCell(3).Text = "1250.00";
item1Row.GetCell(3).HorizontalAlignment = PdfCellHorizontalAlignment.Right;
item1Row.GetCell(4).Text = "2500.00";
item1Row.GetCell(4).HorizontalAlignment = PdfCellHorizontalAlignment.Right;

// Item 2
var item2Row = itemsTable.AddRow();
item2Row.GetCell(0).Text = "2";
item2Row.GetCell(1).Text = "USB-C Docking Station";
item2Row.GetCell(2).Text = "1";
item2Row.GetCell(2).HorizontalAlignment = PdfCellHorizontalAlignment.Right;
item2Row.GetCell(3).Text = "4500.00";
item2Row.GetCell(3).HorizontalAlignment = PdfCellHorizontalAlignment.Right;
item2Row.GetCell(4).Text = "4500.00";
item2Row.GetCell(4).HorizontalAlignment = PdfCellHorizontalAlignment.Right;

// 3. Totals Section
double subtotal = 7000.00;
double cgst = subtotal * 0.09; // 9% CGST
double sgst = subtotal * 0.09; // 9% SGST
double total = subtotal + cgst + sgst;

var totalsTable = page1.AddTable(350, 550, 212);
totalsTable.SetColumnWidths(110, 102);

totalsTable.GetOrCreateCell(0, 0).Text = "Subtotal";
var subtotalValueCell = totalsTable.GetOrCreateCell(0, 1);
subtotalValueCell.Text = subtotal.ToString("F2");
subtotalValueCell.HorizontalAlignment = PdfCellHorizontalAlignment.Right;

totalsTable.GetOrCreateCell(1, 0).Text = "CGST @ 9%";
var cgstValueCell = totalsTable.GetOrCreateCell(1, 1);
cgstValueCell.Text = cgst.ToString("F2");
cgstValueCell.HorizontalAlignment = PdfCellHorizontalAlignment.Right;

totalsTable.GetOrCreateCell(2, 0).Text = "SGST @ 9%";
var sgstValueCell = totalsTable.GetOrCreateCell(2, 1);
sgstValueCell.Text = sgst.ToString("F2");
sgstValueCell.HorizontalAlignment = PdfCellHorizontalAlignment.Right;

var totalCell = totalsTable.GetOrCreateCell(3, 0);
totalCell.Text = "Total";
totalCell.BackgroundColor = PdfColor.LightGray;

var totalValueCell = totalsTable.GetOrCreateCell(3, 1);
totalValueCell.Text = total.ToString("F2");
totalValueCell.HorizontalAlignment = PdfCellHorizontalAlignment.Right;
totalValueCell.BackgroundColor = PdfColor.LightGray;

// 4. Terms and Conditions
page1.AddText("Terms & Conditions:", 50, 450, 10);
page1.AddText("1. Payment due within 30 days.", 50, 438, 9);
page1.AddText("2. Goods once sold will not be taken back.", 50, 426, 9);

pdfDocument.Save(pdfFilePath);

Console.WriteLine("End: Unlimited PDF");
Console.ReadLine();
```

### Screenshot

<img width="1272" height="903" alt="image" src="https://github.com/user-attachments/assets/1e4dc378-1cc6-4d06-9178-48f2377890d2" />

## Add images

Load a JPEG or PNG once, then place it anywhere on any page, or inside table cells. Each image is stored once in the PDF, however many times it is placed, and one loaded image can be shared by many documents, including documents built in parallel.

```cs
using UnlimitedPDF;

// Load from a file, a Stream, or a byte array; async loading takes a cancellation token.
var logo = PdfImage.FromFile("logo.png");
var photo = await PdfImage.FromStreamAsync(uploadStream);

var pdfDocument = new PdfDocument();
var page = pdfDocument.AddPage();

// x and y are the image's bottom-left corner, in points from the page's bottom-left corner.
// With no size, images draw at 96 pixels per inch; with one size, the other keeps the aspect ratio.
page.AddImage(logo, 492, 762, width: 70);

// With both sizes the image stretches to fill the box, or fits inside it with Contain.
page.AddImage(photo, 50, 560, 150, 100, new PdfImagePlacementOptions { Fit = PdfImageFit.Contain });

// Lengths are always points; convert from millimeters, centimeters, inches, or pixels.
page.AddImage(photo, PdfUnits.FromMillimeters(20), PdfUnits.FromMillimeters(20), width: PdfUnits.FromInches(2));

// A cell holds an ordered list of text and images, drawn top to bottom.
// AtLeast rows grow to fit their content; Exactly rows (the default) keep their height and shrink images.
var table = page.AddTable(50, 500, 300);
table.SetColumnWidths(100, 200);
table.RowHeightMode = PdfRowHeightMode.AtLeast;
table.GetOrCreateCell(0, 0).SetContent(photo, "Laptop", "SKU 123");
table.GetOrCreateCell(0, 1).Text = "Image above two lines of text";

pdfDocument.Save("images.pdf");
```

- Supported formats: JPEG (baseline and progressive; grayscale, RGB, CMYK) and PNG (every color type and bit depth, interlaced or not, with transparency).
- Photos honor their EXIF orientation. Turn this off with `PdfImageLoadOptions.ApplyExifOrientation = false`.
- Bad input fails at load. Unsupported formats, damaged data, and images over the pixel limit throw `PdfImageException`. The limit is 100,000,000 pixels by default; change it with `PdfImageLoadOptions.MaxPixels` or `PdfImage.DefaultMaxPixels`.

## Tables across pages

A table that runs past the bottom of the page continues on a new page by itself. Rows are never split, and the new pages are added right after the table's page.

```cs
using UnlimitedPDF;

// Margins apply to every page; both default to 0 (the page edges).
var pdfDocument = new PdfDocument { MarginTop = 40, MarginBottom = 50 };
var page = pdfDocument.AddPage();

var table = page.AddTable(40, 760, 515);
table.SetColumnWidths(60, 255, 200);

// Mark the first row as a header and draw it again at the top of every new page.
table.HeaderRowCount = 1;
table.RepeatHeaderRows = true;
table.GetOrCreateCell(0, 0).Text = "No.";
table.GetOrCreateCell(0, 1).Text = "Name";
table.GetOrCreateCell(0, 2).Text = "Department";

for (int i = 1; i <= 200; i++)
{
    table.GetOrCreateCell(i, 0).Text = i.ToString();
    table.GetOrCreateCell(i, 1).Text = "Employee " + i;
    table.GetOrCreateCell(i, 2).Text = "Engineering";
}

pdfDocument.Save("directory.pdf");
```

- A row taller than the space between the margins, or header rows that cannot fit with one data row, make `Save` throw before anything is written.
- Tables that start on the same page share their continuation pages, so side-by-side tables stay side by side.
- Content you place at coordinates is never moved. To put content after a table, add both to the body flow (see [Headers, footers & flow](#headers-footers--flow)).

## Headers, footers & flow

Give every page a header and a footer, and let body content flow from page to page. Each body item stacks below the previous one, and the library adds pages as the content needs them.

```cs
using UnlimitedPDF;

var pdfDocument = new PdfDocument { MarginTop = 30, MarginBottom = 30, MarginLeft = 40, MarginRight = 40 };

// Drawn on every page. Offsets are measured from the band's top-left corner; content past the band is clipped.
pdfDocument.Header.Height = 40;
pdfDocument.Header.AddText("Quarterly report", 0, 8, 14);
pdfDocument.Header.AddRectangle(0, 36, 515, 1, PdfColor.Black);
pdfDocument.Footer.Height = 20;
pdfDocument.Footer.AddText("Page {page} of {pages}", 0, 4, 9, PdfTextAlignment.Right);

// The first page gets its own, taller header.
var first = pdfDocument.AddPageSettings(PdfPageTarget.First);
first.Header.Height = 70;
first.Header.AddText("Quarterly report", 0, 10, 24, PdfTextAlignment.Center);

// Body items stack below each other with a spacing and continue on new pages by themselves.
var body = pdfDocument.Body;
pdfDocument.BodyMarginTop = 12;
body.AddText("Employees", fontSize: 14, keepWithNext: true);

var table = body.AddTable(515, spacing: 6);
table.SetColumnWidths(60, 255, 200);
table.HeaderRowCount = 1;
table.RepeatHeaderRows = true;
table.GetOrCreateCell(0, 0).Text = "No.";
table.GetOrCreateCell(0, 1).Text = "Name";
table.GetOrCreateCell(0, 2).Text = "Department";

for (int i = 1; i <= 120; i++)
{
    table.GetOrCreateCell(i, 0).Text = i.ToString();
    table.GetOrCreateCell(i, 1).Text = "Employee " + i;
    table.GetOrCreateCell(i, 2).Text = "Engineering";
}

// A block moves to the next page as one piece, right after the table ends.
var signOff = body.AddBlock(spacing: 20);
signOff.AddRectangle(515, 1, PdfColor.Black);
signOff.AddText("Approved by: ____________________", spacing: 10);

pdfDocument.Save("report.pdf");
```

- `{page}` and `{pages}` show each page's number and the page count; `Header.SetPageContent(...)` and `Footer.SetPageContent(...)` set the content per page instead.
- Page settings for `First`, `Last`, `Odd`, `Even`, or a page number change a band's height, visibility, or content, the body margins, or the side margins. `Last` page settings may change content only.
- `keepWithNext` keeps an item, such as a heading, on the page of the item after it. Anything taller than an empty body makes `Save` throw before anything is written.
- `Body.RemainingHeight` is the room left where the next item lands. If what it measured changes afterwards, `Save` throws instead of writing a different layout.
- `Body.MaxPages` caps the page count; content that needs more pages makes `Save` throw, naming what does not fit.
- `AddPage` still works: it starts a new page in the flow, and its page-coordinate content is drawn where you place it.
- Text is aligned with the font's real character widths. It does not wrap.
- Documents that use none of these features are written exactly as before.

## Merge PDFs

Join existing PDFs into one file, or add their pages to a document you build. Each file is loaded and checked once with `PdfFile`, from a path, a stream, or bytes.

```cs
using UnlimitedPDF;

// Join whole files in one call. Each file gets a top-level bookmark.
var report = PdfFile.FromFile("report.pdf");
var invoice = PdfFile.FromFile("invoice.pdf");
PdfOperations.Merge("merged.pdf", report, invoice);

// Or build a cover page and choose page ranges.
var pdfDocument = new PdfDocument();
pdfDocument.Footer.Height = 20;
pdfDocument.Footer.AddText("Page {page} of {pages}", 0, 4, 9);
pdfDocument.AddPage().AddText("Quarterly pack", 72, 700, 24);
pdfDocument.AddPages(report, firstPage: 2, lastPage: 3, bookmarkTitle: "Quarterly report");
pdfDocument.AddPages(invoice);
pdfDocument.Save("pack.pdf");
```

- Imported pages keep their size, rotation, content, links, comments, layers, and bookmarks. Filled form fields keep their look but are no longer editable. Document metadata, attachments, and scripts are not copied.
- Imported pages take no new content and get no header, footer, or page settings, but they count in `{page}` and `{pages}` and act as a page break in the body flow.
- PDF 1.0 to 2.0 files load, including compressed cross-reference and object streams and incremental updates. A broken cross-reference table is repaired by scanning the file.
- Bad input fails at load with `PdfFileException`: encrypted files (`Encrypted`; they are not opened), damaged data (`Damaged`), files without pages (`Empty`), and files over a limit (`TooLarge`). The limits are 500 MB per file, 100 MB per decompressed stream, and 5,000,000 objects; change them with `PdfFileLoadOptions` or `PdfFile.DefaultMaxFileSize`, `PdfFile.DefaultMaxStreamSize`, and `PdfFile.DefaultMaxObjectCount`.
- Merging works in the browser too: load uploads with `PdfFile.FromStreamAsync`.

## Split PDFs

Split an existing PDF into smaller files: every few pages, or one file per page range. The file is loaded once with `PdfFile`, and each part is written as soon as it is built.

```cs
using System.IO.Compression;
using UnlimitedPDF;

var report = PdfFile.FromFile("report.pdf");

// One file per page, into a folder: pages/report-01.pdf, pages/report-02.pdf, ...
PdfOperations.Split(report, 1, "pages");

// One file per page range, in the order given. Ranges may overlap and leave pages out.
PdfOperations.Split(report, new[] { new PdfPageRange(1, 3), new PdfPageRange(8, 10) }, "chapters");

// Or write each part to a stream, for example a zip entry. Streams are left open, and a zip writes one entry at a
// time, so close each entry when the next part asks for its stream.
using (var zip = new ZipArchive(File.Create("report-parts.zip"), ZipArchiveMode.Create))
{
    Stream? entry = null;
    PdfOperations.Split(report, 4, part =>
    {
        entry?.Dispose();
        return entry = zip.CreateEntry(part.FileName).Open();
    });
    entry?.Dispose();
}
```

- Every N pages makes parts of N pages from the first page; the last part is shorter when the pages don't divide evenly. `PdfPageRange` pages are 1-based and inclusive. Every overload has a `SplitAsync` twin that takes a `CancellationToken`.
- Folder parts are named after the file and numbered, padded to the part count (`report-1.pdf` to `report-9.pdf`, or `report-01.pdf` to `report-12.pdf`); files loaded from a stream or bytes are named `part-1.pdf` and so on. The folder is created when missing, and existing files with those names are overwritten. Each call returns the parts (`PdfSplitPart`: `Number`, `FirstPage`, `LastPage`, `FileName`).
- Each part's pages keep their size, rotation, content, links, comments, and layers, and the part keeps the file's bookmarks that point into it, at their own levels. Links and bookmarks to pages in other parts are removed. Document metadata, attachments, and scripts are not copied.
- Parts are written one at a time. If a part can't be written, the parts before it are kept, its half-written file is removed, and `PdfSplitException` says which part failed (`PartNumber`) and how many were written (`PartsWritten`). An existing file with the failed part's name is removed too. A cancelled `SplitAsync` keeps the finished parts in the same way.
- Each page's fonts and images are copied as the file lists them. A file that lists all of its fonts and images for every page gives large parts.

## HTML to PDF

Convert HTML into PDF with the library's own renderer: no browser, no extra package, and it runs wherever .NET runs, including Blazor WebAssembly. Load the HTML once with `PdfHtml`, then convert it in one call or add it to a document next to other content.

```cs
using UnlimitedPDF;

// One call: an HTML file with its images next to it, to A4 with half-inch margins.
var invoice = PdfHtml.FromFile("invoice.html");
PdfOperations.ConvertHtml(invoice, "invoice.pdf");
foreach (var warning in invoice.Warnings) Console.WriteLine(warning); // what was skipped, if anything

// Or mix HTML with other content: page numbers in the footer, headings as bookmarks.
var document = new PdfDocument { MarginTop = 36, MarginBottom = 36, MarginLeft = 36, MarginRight = 36 };
document.Footer.Height = 20;
document.Footer.AddText("Page {page} of {pages}", 0, 0);
document.Body.AddHtml(PdfHtml.FromString("<h1>Q3 Report</h1><p>Output rose <b>6%</b>.</p>"), headingBookmarks: true);
document.Save("report.pdf");

// Images from anywhere else come only through your resolver: the library never uses the network.
var options = new PdfHtmlLoadOptions
{
    ResourceResolver = reference => reference == "https://cdn.example.com/logo.png" ? File.OpenRead("logo.png") : null
};
var banner = PdfHtml.FromString("<img src='https://cdn.example.com/logo.png'>", options);
```

- Supported: block and inline layout with wrapping and justified text; headings, paragraphs, `div`, `span`, `blockquote`, `pre`, and the HTML5 section elements; nested `ul`, `ol`, and `dl` lists with every common marker style and `start`, `reversed`, and `value`; tables nested to any depth with `colspan`, `rowspan`, captions, and both border models; PNG and JPEG images; `<style>`, `<link rel="stylesheet">`, and `style` attributes with type, class, id, attribute, descendant, and child selectors; colors, fonts, sizes, margins, padding, borders, backgrounds, widths, and page breaks. HTML is laid out as a document, not as a browser shows it: scripts, flexbox, grid, floats, positioning, transforms, rounded corners, background images, and web fonts are not supported, and each one skipped is listed in `Warnings`.
- Text uses the standard PDF fonts: `serif` is Times, `monospace` is Courier, and other families are Helvetica, each with bold and italic. Western European characters such as `é`, `ñ`, and `€` are supported; other characters are written as `?`.
- Pages: the HTML uses the document's page size and margins (A4 with 36 pt margins for `ConvertHtml`). Lines and images never split across pages, table rows move to the next page whole, `thead` rows repeat on every page, and a row taller than a page continues cell by cell. `break-before`, `break-after`, `break-inside: avoid`, and the `page-break-*` properties are honored. Content wider than the page narrows to fit; anything that still doesn't fit is clipped and reported.
- Resources: `data:` URIs, and relative paths inside the base folder (the HTML file's folder, or `BaseFolder`). Paths that leave that folder, absolute paths, and symbolic links are never read. Anything else goes to your `ResourceResolver` (or `ResourceResolverAsync` with the `Async` load methods). Images and stylesheets that can't be loaded are skipped, an image showing its `alt` text instead.
- Links: `http`, `https`, and `mailto` links are clickable, and `#id` links jump inside the document; other link types keep their text only. `headingBookmarks: true` turns `h1` to `h6` into bookmarks.
- Problems: anything skipped is listed in `PdfHtml.Warnings` (`Kind`, `Source`, `Line` or `PageNumber`). Set `FailOnWarning` to throw `PdfHtmlException` on the first one instead, for example in template tests. Limits keep hostile input in check: 50 MB of HTML, 256 levels of nesting, 50 MB per image or stylesheet, and 200 MB in total, changeable with the `PdfHtml.Default*` properties or per load.
- Known limitations: these come out differently from a browser. The playground's Common problems demo shows examples of the flexbox and grid, background image, SVG and WebP, character, and `pre` limitations, next to common HTML to PDF problems that this renderer handles, such as rows cut at page breaks, header rows that don't repeat, and long URLs.
  - Flexbox and grid: items are laid out as stacked blocks.
  - Floats and positioning: ignored; elements stay in the normal flow.
  - Background images: skipped; background colors are drawn.
  - Images other than PNG and JPEG, such as SVG and WebP: their `alt` text is shown.
  - Characters outside Western European, such as Cyrillic, Chinese, and emoji: written as `?`.
  - `pre` lines don't wrap, as in a browser, so a line wider than the page is clipped; use `white-space: pre-wrap` to wrap it.

## Upgrading to 1.0.0

1.0.0 renames part of the API so that names follow one convention and are easier to guess. Old names are removed, so code written for 0.0.x reports compile errors until it uses the new names. Generated PDFs are unchanged. Every public type is now in the `UnlimitedPDF` namespace, so `using UnlimitedPDF;` is the only `using` you need.

| 0.0.x name | 1.0.0 name |
|---|---|
| `using UnlimitedPDF.Enums;` | delete it (types are in `UnlimitedPDF`) |
| `using UnlimitedPDF.Models.Table;` | delete it (types are in `UnlimitedPDF`) |
| `using UnlimitedPDF.Extensions;` | delete it (types are in `UnlimitedPDF`) |
| `PdfPageBuilder` | `PdfPage` |
| `PdfTableRow` | `PdfRow` |
| `PdfTableColumn` | `PdfColumn` |
| `PdfTableCell` | `PdfCell` |
| `PdfPageBuilder.AddText(string text, int x, int y, int fontSize, string fontName)` | `PdfPage.AddText(string text, double x, double y, double fontSize = 12)` (no font name; there is one font) |
| `PdfTable.ColCount` | `PdfTable.ColumnCount` |
| `PdfTable.TrimEmptyTrailingRowsAndCols()` | `PdfTable.TrimEmptyTrailingRowsAndColumns()` |
| `PdfTable.EnsureSize(rows, cols)` | `PdfTable.EnsureSize(rows, columns)` |
| `col` parameter of `PdfTable.GetCell`, `GetOrCreateCell`, `SetCell`, `SetText` | `column` (only named arguments change) |
| `new PdfTableRow(index, initialCols)` | `new PdfRow(index, initialColumns)` |
| `colIndex` parameter of `PdfTableRow.GetCell`, `SetCell` | `columnIndex` (only named arguments change) |
| `PdfTableCell.Background` | `PdfCell.BackgroundColor` |
| `PdfTableCell.ColSpan` | `PdfCell.ColumnSpan` |
| `PdfTableCell.HAlign` | `PdfCell.HorizontalAlignment` |
| `PdfTableCell.VAlign` | `PdfCell.VerticalAlignment` |
| `PdfTableCell.TopBorder`, `BottomBorder`, `LeftBorder`, `RightBorder` | `PdfCell.BorderTop`, `BorderBottom`, `BorderLeft`, `BorderRight` |
| `PdfTable.GetEffectiveColumnWidths()`, `NormalizeSpans()`, `IsSameSpanCell(...)` | removed (internal layout helpers) |
| `PdfPageSize.ToMediaBoxString()` | removed (internal PDF syntax); use `ToDimensions()` for the size in points |

## Changes in 1.0.0

- New: merge PDFs (`PdfFile`, `PdfFileLoadOptions`, `PdfFileException`, `PdfDocument.AddPages`, `PdfOperations.Merge`): load existing PDFs, join whole files in one call, or add page ranges next to pages you build.
- New: split PDFs (`PdfOperations.Split`, `PdfOperations.SplitAsync`, `PdfPageRange`, `PdfSplitPart`, `PdfSplitException`): split a PDF every N pages or into one file per page range, written to a folder or to a stream per part.
- New: HTML to PDF (`PdfHtml`, `PdfHtmlLoadOptions`, `PdfHtmlWarning`, `PdfHtmlException`, `PdfBodyFlow.AddHtml`, `PdfOperations.ConvertHtml`, `PdfOperations.ConvertHtmlAsync`, `PdfHtmlConvertOptions`): convert HTML with CSS, nested tables and lists, images, and links into PDF with a built-in renderer, in one call or next to other content.
- New: tables continue on new pages when they pass the bottom of the page (`PdfDocument.MarginTop`, `PdfDocument.MarginBottom`, `PdfTable.HeaderRowCount`, `PdfTable.RepeatHeaderRows`). Tables that fit their page are drawn exactly as before.
- New: headers, footers, and body flow (`PdfDocument.Header`, `PdfDocument.Footer`, `PdfDocument.Body`, `PdfDocument.BodyMarginTop`, `PdfDocument.BodyMarginBottom`, `PdfDocument.AddPageSettings`, `PdfDocument.MarginLeft`, `PdfDocument.MarginRight`), and filled rectangles (`PdfPage.AddRectangle`).
- New: images (`PdfImage`, `PdfPage.AddImage`), images and stacked content in table cells, row height modes (`PdfRowHeightMode`), and unit conversion (`PdfUnits`).
- New: colors from 0–255 channels or hex strings (`PdfColor.FromRgb(0, 172, 140)`, `PdfColor.FromHex("#002A73")`).
- New: .NET 11 support (`net11.0`), alongside .NET 6, 8, 9, and 10.
- Renamed: see [Upgrading to 1.0.0](#upgrading-to-100).
- `PdfCell.Text` now reads and writes the cell's first text item; text-only cells behave as before.
- Table cells with `VerticalAlignment` set to `Top` or `Bottom` are now drawn that way (it had no effect before).
- Text containing a backslash or an unbalanced parenthesis is now escaped wherever it appears (table cells, `PdfPage.AddText`, headers, footers, and the body flow), so it displays as written. Text without those characters is written exactly as before.
- Table numbers are written the same way in every culture, so documents saved under comma-decimal cultures are valid.
