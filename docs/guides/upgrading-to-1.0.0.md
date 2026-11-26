# Upgrading to 1.0.0

[README](../../README.md) · [All guides](README.md) · [Changelog](../../CHANGELOG.md)

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
