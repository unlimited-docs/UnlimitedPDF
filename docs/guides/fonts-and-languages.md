# Fonts and languages

[README](../../README.md) · [All guides](README.md) · [Try it live](https://unlimitedpdf.dev/playground/languages/fonts-and-languages)

Draw text in your own fonts: Hindi, Bengali, Assamese, Gujarati, Punjabi, Odia, Kannada, Malayalam, Telugu, Tamil, Arabic, Persian, Urdu, Pashto, Kurdish (Sorani), Sindhi, Uyghur, Malay (Jawi), Hebrew, Dhivehi, Greek, Russian, Ukrainian, Bulgarian, Serbian, Kazakh, Armenian, Georgian, French, Polish, Turkish, and other languages written in Latin letters, each shaped the way its script needs and written in its own direction. Load a `.ttf` or `.otf` font once with `PdfFont`, then pass it to any text call; characters it doesn't have come from the document's fallback fonts. The PDF embeds only the glyphs it uses, and copied or searched text reads back exactly as written.

Before you start, you need:

- A `.ttf` or `.otf` font that has the letters of each script you write: one font per script, or one that covers them all. The package includes no fonts; ship the font files with your app.
- The right to embed each font in a PDF. Check the font's license; free fonts under the SIL Open Font License, for example, allow it.
- A supported script: Latin, Greek, Cyrillic, Armenian, Georgian, Devanagari, Bengali-Assamese, Gujarati, Gurmukhi, Odia, Kannada, Malayalam, Telugu, Tamil, Arabic, Hebrew, or Thaana.
- A check for characters no font has: `FindMissingCharacters` or `FailOnMissingCharacters`, so none is drawn as `?`.

```cs
using UnlimitedPDF;

var latin = PdfFont.FromFile("fonts/BrandSans-Regular.ttf");
var hindi = PdfFont.FromFile("fonts/BrandDevanagari-Regular.ttf");
var telugu = PdfFont.FromFile("fonts/BrandTelugu-Regular.ttf");

var document = new PdfDocument();
document.FontFallback.Add(hindi); // used for characters the call's font doesn't have
document.FontFallback.Add(telugu);

var page = document.AddPage();
page.AddText("Invoice", latin, 72, 760, 18);
page.AddText("Merci, नमस्ते, ధన్యవాదాలు", latin, 72, 730); // three scripts in one line

// Find characters no font can draw before saving, or make Save fail on them instead of drawing '?'.
var missing = document.FindMissingCharacters("Thank you 🙏", latin); // ["🙏"]
document.FailOnMissingCharacters = true;
document.Save("invoice.pdf");

// HTML: register fonts under the family names your CSS uses.
var options = new PdfHtmlLoadOptions();
options.Fonts.Add("Brand Sans", latin);
options.Fonts.Add("Brand Sans", PdfFont.FromFile("fonts/BrandSans-Bold.ttf"));
options.Fonts.Add("Brand Devanagari", hindi);
var html = PdfHtml.FromString("<p style=\"font-family: 'Brand Sans', 'Brand Devanagari'\">Total <b>₹ 1,250</b> कुल</p>", options);
PdfOperations.ConvertHtml(html, "total.pdf");
```

Right-to-left documents set their direction once; text, alignment, and tables follow it:

```cs
using UnlimitedPDF;

var arabic = PdfFont.FromFile("fonts/BrandArabic-Regular.ttf");

var document = new PdfDocument { TextDirection = PdfTextDirection.RightToLeft };
document.FontFallback.Add(arabic);

document.Body.AddText("فاتورة رقم 42", arabic, fontSize: 18); // starts at the right edge
document.Body.AddText("iPhone 15 السعر", arabic); // still reads from the right
document.Body.AddText("Model X1", new PdfTextOptions { Direction = PdfTextDirection.LeftToRight }); // one left-to-right line

var table = document.Body.AddTable(400); // column 0 is drawn at the right
table.SetText(0, 0, "المنتج", arabic);
table.SetText(0, 1, "السعر", arabic);
document.Save("invoice-ar.pdf");
```

- Fonts: `PdfFont.FromFile`, `FromFileAsync`, `FromStream`, `FromStreamAsync`, and `FromBytes` load `.ttf` and `.otf` fonts with TrueType or CFF outlines. A variable font draws any weight it holds: `new PdfFontLoadOptions { Weight = 700 }`. Font collections (`.ttc`), WOFF, WOFF2, damaged fonts, fonts over 50 MB (`PdfFont.DefaultMaxFileSize`, or `MaxFileSize` per load), and fonts whose license forbids embedding throw `PdfFontException` with a `Reason`. Set `IgnoreEmbeddingRestrictions` only when you hold the rights to embed the font.
- Where: `PdfPage.AddText`, headers and footers, the body flow, flow blocks, table cells (`new PdfCellText(text, font)` or `PdfTable.SetText(row, column, text, font)`), and HTML. Each character takes the call's font, then the `PdfDocument.FontFallback` fonts in order, so one line can mix scripts.
- Scripts: Latin, Greek, Cyrillic, Armenian, and Georgian (accents, ligatures, kerning, each with its own script's font rules), Devanagari, Bengali-Assamese, Gujarati, Gurmukhi, Odia, Kannada, Malayalam, Telugu, and Tamil are shaped with the font's own rules: conjuncts, half forms, reph, vowel signs drawn before their consonant or split around it, and marks placed above and below. Arabic (with Persian, Urdu, Pashto, Sorani, Sindhi, Uyghur, and Jawi letters) takes its joined forms, required ligatures such as Lam with Alef, its marks, and the cursive attachment Nastaliq fonts use; Hebrew points and Thaana vowel signs sit on their letters. Copy, search, and read-aloud return the original text, in reading order.
- Direction: lines are ordered by the Unicode Bidirectional Algorithm, so Latin words, numbers (Western or Arabic-Indic, drawn as typed), and brackets inside Arabic or Hebrew text read correctly. `PdfDocument.TextDirection` sets the base direction; the default, `Auto`, takes each text's first letter, so documents without right-to-left text are unchanged. Override it for one call with `PdfTextOptions.Direction`, for a table with `PdfTable.Direction` (a right-to-left table draws its first column at the right), or for a cell's text with `PdfCellText.Direction`.
- Alignment: `PdfTextAlignment.Start` and `End`, and the same on `PdfCellHorizontalAlignment`, follow the text's direction, and `Start` is the default, so right-to-left text lands at the right edge without an argument. `Left`, `Center`, and `Right` stay where they say.
- Missing characters: a character no font has is drawn as `?`. `FindMissingCharacters` lists them before you save; `FailOnMissingCharacters` makes `Save` throw `PdfTextException` (the character, its text, and its page) before anything is written; HTML adds a `MissingCharacter` warning. Right-to-left letters of scripts that aren't shaped, such as Syriac or N'Ko, are reported the same way even when the font has them, because they would be drawn without their joining.
- Without a font: text uses the built-in Helvetica as before, and Western European characters such as `é`, `ñ`, `€`, dashes, and curly quotes are drawn as written. Other Latin letters, such as Polish `ł`, Czech `č`, Romanian `ș`, and Turkish `ğ` and `ı`, and every other script need a font of your own. Documents with only ASCII text are written byte for byte as before.
- HTML: `font-family` names map to the fonts registered in `PdfHtmlLoadOptions.Fonts`, matched by weight and style; register one font per weight and style, or one variable font, which draws CSS weights rounded to the nearest hundred (on the same side of 600, so bold stays bold). Each character takes the first listed family that has it, then the fallback fonts (`PdfDocument.FontFallback`, or `PdfHtmlConvertOptions.FontFallback` for `ConvertHtml`), then the standard fonts. When a bold or italic font is missing, the nearest registered one is used with a `MissingFontFace` warning; `SynthesizeMissingStyles` draws the regular font thickened or slanted instead. `@font-face` rules are ignored with a `FontFaceIgnored` warning, and their font files are never read. `text-transform` follows the `lang` attribute for Turkish and Azerbaijani, so `istanbul` in uppercase is `İSTANBUL`. The `dir` attribute (`ltr`, `rtl`, `auto`), CSS `direction`, `<bdi>`, and `<bdo>` set direction; `text-align: start` and `end`, table columns, and list markers follow it; HTML keeps its own direction rather than the document's.
- Known limitations:
  - Right-to-left scripts other than Arabic, Hebrew, and Thaana, such as Syriac, N'Ko, and Samaritan, are drawn without their script's shaping and reported as missing; Chinese, Japanese, and Korean are not supported.
  - HTML `unicode-bidi` is not supported, and Arabic is justified by widening spaces, not by stretching letters.
  - Letter forms a font keeps for one language, such as Bulgarian or Serbian Cyrillic, or Marathi Devanagari, aren't chosen; the font's default forms are drawn.
  - Font collections (`.ttc`) and web fonts (WOFF, WOFF2) can't be loaded; convert them to `.ttf` or `.otf`.
  - HTML `@font-face` rules are ignored; register fonts in code instead.
