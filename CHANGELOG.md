# Changelog

What changed in each release of the [UnlimitedPDF package](https://www.nuget.org/packages/UnlimitedPDF). Upgrading from 0.0.x? See [Upgrading to 1.0.0](docs/guides/upgrading-to-1.0.0.md).

## 1.1.0

- New: your own fonts and more languages (`PdfFont`, `PdfFontLoadOptions`, `PdfFontException`, `PdfFontErrorReason`): text in Hindi, Telugu, Tamil, and languages written in Latin letters, shaped by the font's rules, on every text call (`PdfPage.AddText`, headers, footers, the body flow, flow blocks, and table cells with `PdfCellText` and `PdfTable.SetText`). PDFs embed only the glyphs they use. See [Fonts and languages](docs/guides/fonts-and-languages.md).
- New: fallback fonts and missing characters (`PdfDocument.FontFallback`, `PdfDocument.FindMissingCharacters`, `PdfDocument.FailOnMissingCharacters`, `PdfTextException`).
- New: fonts in HTML (`PdfHtmlLoadOptions.Fonts`, `PdfHtmlFontCollection`, `PdfHtmlLoadOptions.SynthesizeMissingStyles`, `PdfHtmlConvertOptions.FontFallback`), and the warning kinds `MissingCharacter`, `MissingFontFace`, and `FontFaceIgnored`.
- New: extract and remove pages (`PdfOperations.Extract`, `ExtractAsync`, `Remove`, `RemoveAsync`): write the pages you choose to a new PDF in your order, or write a PDF without them, keeping bookmarks, links between the kept pages, and the file's title and author, with only the fonts and images those pages use. See [Extract and remove pages](docs/guides/extract-and-remove-pages.md).
- New: page lists as text (`PdfPageRange.Parse`), such as `2, 5, 7-9` or `9-7`, for `Extract`, `Remove`, and `Split`, and `PdfDocument.AddPages(file, ranges, bookmarkTitle)`, which adds the chosen pages to a document under one bookmark.
- Changed: text drawn without a font now shows Western European characters such as `é`, `ü`, `€`, dashes, and curly quotes instead of `?`. Documents with only ASCII text are written exactly as before.
- Changed: HTML reports each character it draws as `?` with a `MissingCharacter` warning, so `FailOnWarning` now fails on such characters, and reports `@font-face` as `FontFaceIgnored` instead of `UnsupportedProperty`.
- Changed: in HTML, a word wider than its line is split only between whole characters, so an emoji or a letter with its accent stays together.
- Changed: a call that passes `null` as a page number by position, such as `AddPages(file, null, 7)`, no longer compiles because of the new `AddPages` overload; name the argument instead: `AddPages(file, lastPage: 7)`. Built apps keep working.
- Fixed: table cell positions, sizes, and border widths are always written as plain decimal numbers. A very small or very large value used to be written in exponent form, such as `1E-05`, which PDF readers don't accept.
- Fixed: loading a damaged PDF to merge or split stays fast and small. Compressed data with out-of-range predictor settings is reported as a damaged file, and oversized named-destination names are skipped, instead of taking large amounts of memory and time.
- The package includes `THIRD-PARTY-NOTICES.txt`, the license of the Unicode data the script tables are built from.

## 1.0.1

- Fixed: text didn't show when a PDF was opened on an iPhone or iPad (Safari, Chrome, and other apps that use Apple's PDF viewer); only lines, fills, and images appeared. Every page now names the font its text uses. PDFs look the same in every other viewer, and the API is unchanged.

## 1.0.0

- New: merge PDFs (`PdfFile`, `PdfFileLoadOptions`, `PdfFileException`, `PdfDocument.AddPages`, `PdfOperations.Merge`): load existing PDFs, join whole files in one call, or add page ranges next to pages you build.
- New: split PDFs (`PdfOperations.Split`, `PdfOperations.SplitAsync`, `PdfPageRange`, `PdfSplitPart`, `PdfSplitException`): split a PDF every N pages or into one file per page range, written to a folder or to a stream per part.
- New: HTML to PDF (`PdfHtml`, `PdfHtmlLoadOptions`, `PdfHtmlWarning`, `PdfHtmlException`, `PdfBodyFlow.AddHtml`, `PdfOperations.ConvertHtml`, `PdfOperations.ConvertHtmlAsync`, `PdfHtmlConvertOptions`): convert HTML with CSS, nested tables and lists, images, and links into PDF with a built-in renderer, in one call or next to other content.
- New: tables continue on new pages when they pass the bottom of the page (`PdfDocument.MarginTop`, `PdfDocument.MarginBottom`, `PdfTable.HeaderRowCount`, `PdfTable.RepeatHeaderRows`). Tables that fit their page are drawn exactly as before.
- New: headers, footers, and body flow (`PdfDocument.Header`, `PdfDocument.Footer`, `PdfDocument.Body`, `PdfDocument.BodyMarginTop`, `PdfDocument.BodyMarginBottom`, `PdfDocument.AddPageSettings`, `PdfDocument.MarginLeft`, `PdfDocument.MarginRight`), and filled rectangles (`PdfPage.AddRectangle`).
- New: images (`PdfImage`, `PdfPage.AddImage`), images and stacked content in table cells, row height modes (`PdfRowHeightMode`), and unit conversion (`PdfUnits`).
- New: colors from 0–255 channels or hex strings (`PdfColor.FromRgb(0, 172, 140)`, `PdfColor.FromHex("#002A73")`).
- New: .NET 11 support (`net11.0`), alongside .NET 6, 8, 9, and 10.
- Renamed: see [Upgrading to 1.0.0](docs/guides/upgrading-to-1.0.0.md).
- `PdfCell.Text` now reads and writes the cell's first text item; text-only cells behave as before.
- Table cells with `VerticalAlignment` set to `Top` or `Bottom` are now drawn that way (it had no effect before).
- Text containing a backslash or an unbalanced parenthesis is now escaped wherever it appears (table cells, `PdfPage.AddText`, headers, footers, and the body flow), so it displays as written. Text without those characters is written exactly as before.
- Table numbers are written the same way in every culture, so documents saved under comma-decimal cultures are valid.
