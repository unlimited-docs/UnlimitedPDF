using UnlimitedPDF.Models.Internal.Fonts;
using UnlimitedPDF.Models.Internal.Html.Css;
using UnlimitedPDF.Models.Internal.Html.Parsing;

namespace UnlimitedPDF.Models.Internal.Html.Layout;

/// <summary>
/// Inline layout (WR-038, AC-08): line boxes from text, line breaks, images, and inline-blocks.
/// </summary>
internal sealed partial class HtmlLayoutEngine
{
    #region Fields and Constants

    // Ascent and descent of the standard fonts' content area, as fractions of the font size.
    private const double Ascent = 0.8;
    private const double Descent = 0.2;

    private readonly HashSet<string> _reportedLinks = new(StringComparer.Ordinal);

    #endregion

    #region Methods

    #region Private Methods

    private static string ApplyTransform(string text, CssTextTransform transform) => transform switch
    {
        CssTextTransform.Uppercase => text.ToUpperInvariant(),
        CssTextTransform.Lowercase => text.ToLowerInvariant(),
        CssTextTransform.Capitalize => string.Concat(text.Select((c, i) => i == 0 || char.IsWhiteSpace(text[i - 1]) ? char.ToUpperInvariant(c) : c)),
        _ => text
    };

    // Breaks the tokens into lines that fit the width, wrapping at spaces and splitting a word only when it alone is
    // wider than its line.
    private List<Line> BreakLines(List<Token> tokens, double width, double indent)
    {
        var lines = new List<Line>();
        var line = new Line();
        double available = width - indent;

        void Finish(bool forced)
        {
            while (line.Tokens.Count > 0 && line.Tokens[line.Tokens.Count - 1] is { Kind: TokenKind.Space, Collapsible: true } trailing)
            {
                line.Width -= trailing.Width;
                line.Tokens.RemoveAt(line.Tokens.Count - 1);
            }
            line.Forced = forced;
            lines.Add(line);
            line = new Line();
            available = width;
        }

        foreach (var token in tokens)
        {
            switch (token.Kind)
            {
                case TokenKind.Break:
                    line.Tokens.Add(token);
                    Finish(forced: true);
                    break;
                case TokenKind.Space:
                    if (token.Collapsible && line.Tokens.All(t => t.Kind == TokenKind.Space || (t.Kind == TokenKind.Word && t.Text.Length == 0))) continue;
                    line.Tokens.Add(token);
                    line.Width += token.Width;
                    break;
                default:
                    bool wraps = token.Item.Style.WhiteSpace is CssWhiteSpace.Normal or CssWhiteSpace.PreWrap or CssWhiteSpace.PreLine;
                    bool hasContent = line.Tokens.Any(t => t.Kind != TokenKind.Space && t.Width > 0);
                    if (hasContent && wraps && line.Width + token.Width > available + Tolerance && LastIsBreakOpportunity(line))
                        Finish(forced: false);
                    if (token.Kind == TokenKind.Word && wraps && token.Width > available + Tolerance && token.Text.Length > 1 && !line.Tokens.Any(t => t.Kind != TokenKind.Space && t.Width > 0))
                    {
                        foreach (var piece in SplitWord(token, available, width))
                        {
                            if (line.Tokens.Any(t => t.Width > 0)) Finish(forced: false);
                            line.Tokens.Add(piece);
                            line.Width += piece.Width;
                        }
                        break;
                    }
                    line.Tokens.Add(token);
                    line.Width += token.Width;
                    break;
            }
        }
        if (line.Tokens.Count > 0 || lines.Count == 0) Finish(forced: false);
        return lines;
    }

    private static string? Classify(string href, out string? targetId)
    {
        targetId = null;
        string trimmed = href.Trim();
        if (trimmed.StartsWith("#", StringComparison.Ordinal))
        {
            targetId = Uri.UnescapeDataString(trimmed.Substring(1));
            return null;
        }
        int colon = trimmed.IndexOf(':');
        string scheme = colon > 0 ? trimmed.Substring(0, colon).ToLowerInvariant() : string.Empty;
        if (scheme is not ("http" or "https" or "mailto")) return null;

        // URI strings are ASCII: anything else is percent-encoded as UTF-8.
        var sb = new StringBuilder();
        foreach (byte b in Encoding.UTF8.GetBytes(trimmed))
        {
            if (b > 32 && b < 127) sb.Append((char)b);
            else sb.Append('%').Append(b.ToString("X2", CultureInfo.InvariantCulture));
        }
        return sb.ToString();
    }

    private (double Width, double Height) ImageSize(HtmlImageBox box, double containing)
    {
        var s = box.Style;
        double naturalWidth = box.Image.PixelWidth * 0.75, naturalHeight = box.Image.PixelHeight * 0.75;
        double? w = s.Width is { } sw ? Resolve(sw, containing) : null;
        double? h = s.Height is { Unit: not CssUnit.Percent } sh ? Resolve(sh, 0) : null;
        double width = w ?? (h is { } hh && naturalHeight > 0 ? hh * naturalWidth / naturalHeight : naturalWidth);
        double height = h ?? (w is { } ww && naturalWidth > 0 ? ww * naturalHeight / naturalWidth : naturalHeight);
        double ratio = width > 0 ? height / width : 0;
        if (s.MaxWidth is { } max && width > Resolve(max, containing)) { width = Resolve(max, containing); if (h is null) height = width * ratio; }
        if (s.MinWidth is { } min && width < Resolve(min, containing)) { width = Resolve(min, containing); if (h is null) height = width * ratio; }

        // Narrower than the line and no taller than a page body, keeping the aspect ratio (D-016, D-011).
        if (width > containing + Tolerance && width > 0) { height *= containing / width; width = containing; }
        if (!_measuring)
        {
            var room = _atTop ? Room(_page) : Room(_page + 1);
            double body = room.Top - room.Bottom;
            if (height > body + Tolerance && height > 0) { width *= body / height; height = body; }
        }
        return (Math.Max(0, width), Math.Max(0, height));
    }

    private static bool LastIsBreakOpportunity(Line line) =>
        line.Tokens.Count > 0 && (line.Tokens[line.Tokens.Count - 1].Kind is TokenKind.Space or TokenKind.Atomic || line.Tokens[line.Tokens.Count - 1].Kind == TokenKind.Word);

    private void LayoutInline(BlockBox box, double left, double width, Action mark)
    {
        var s = box.Style;
        var tokens = Tokenize(box, width);
        double indent = Resolve(s.TextIndent, width);
        var lines = BreakLines(tokens, width, indent);
        bool clipReported = false;

        for (int index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            bool first = index == 0;

            // The line box: the block's strut and each item's inline box, around a shared baseline.
            double strutAbove = StrutAbove(s), strutBelow = s.LineHeight - strutAbove;
            double above = strutAbove, below = strutBelow;
            foreach (var token in line.Tokens)
            {
                var (a, b) = Extent(token, s);
                above = Math.Max(above, a);
                below = Math.Max(below, b);
            }
            double height = above + below;
            Flush(height);
            if (first) mark();
            double top = _y;
            double baseline = top - above;
            _y -= height;
            _atTop = false;
            var fragment = Current;
            fragment.Bottom = Math.Min(fragment.Bottom, _y);

            foreach (var marker in _pendingMarkers) DrawMarker(marker, baseline);
            _pendingMarkers.Clear();

            double lineLeft = left + (first ? indent : 0);
            double lineWidth = width - (first ? indent : 0);
            double free = lineWidth - line.Width;
            bool last = index == lines.Count - 1 || line.Forced;
            int spaces = line.Tokens.Count(t => t.Kind == TokenKind.Space && t.Collapsible);
            double extra = s.TextAlign == CssTextAlign.Justify && !last && spaces > 0 && free > 0 ? free / spaces : 0;
            double x = lineLeft + (s.TextAlign switch
            {
                CssTextAlign.Right => free,
                CssTextAlign.Center => free / 2,
                _ => 0
            });
            if (free < 0) x = lineLeft;

            var texts = new List<HtmlCommand>();
            var backgrounds = new List<HtmlCommand>();
            int i = 0;
            while (i < line.Tokens.Count)
            {
                var token = line.Tokens[i];
                foreach (var start in token.Starts)
                {
                    if (start.Attribute("id") is { } id) texts.Add(new MarkCommand(id, 0, null, x, top));
                }

                if (token.Kind == TokenKind.Break) { i++; continue; }
                if (token.Kind == TokenKind.Atomic)
                {
                    PlaceAtomic(token, x, baseline, s, texts);
                    AddLink(token.Item, x, baseline - below, token.Width, height, texts);
                    x += token.Width;
                    i++;
                    continue;
                }

                // A run: words and spaces of one item, drawn as one text command.
                var item = token.Item;
                var run = new StringBuilder();
                double runWidth = 0;
                int runSpaces = 0;
                int j = i;
                while (j < line.Tokens.Count && line.Tokens[j].Kind is TokenKind.Word or TokenKind.Space && line.Tokens[j].Item == item && (j == i || line.Tokens[j].Starts.Count == 0))
                {
                    var part = line.Tokens[j];
                    run.Append(part.Text);
                    runWidth += part.Width;
                    if (part.Kind == TokenKind.Space && part.Collapsible) runSpaces++;
                    j++;
                }
                // Justified spaces at the end of a run belong to it.
                runWidth += runSpaces * extra;
                var style = item.Style;
                var font = Font(style);
                double size = style.FontSize;
                double shift = Shift(style, s);
                double runBaseline = baseline + shift;
                if (run.Length > 0)
                {
                    if (item.Background is { } bg) backgrounds.Add(new FillCommand(x, runBaseline - Descent * size, runWidth, size, bg));
                    texts.Add(new TextCommand(font, size, style.Color, x, runBaseline, WinAnsiEncoding.Encode(run.ToString()), extra, style.LetterSpacing));
                    double thickness = Math.Max(0.5, size * 0.05);
                    if (style.Underline) texts.Add(new LineCommand(x, runBaseline - size * 0.12, x + runWidth, runBaseline - size * 0.12, thickness, style.Color, HtmlBorderStyle.Solid));
                    if (style.LineThrough) texts.Add(new LineCommand(x, runBaseline + size * 0.28, x + runWidth, runBaseline + size * 0.28, thickness, style.Color, HtmlBorderStyle.Solid));
                    AddLink(item, x, runBaseline - Descent * size, runWidth, size, texts);
                }
                x += runWidth;
                i = j;
            }
            // A line that cannot wrap and passes the body edge is clipped there (D-016).
            bool overflow = lineLeft + line.Width > _width + Tolerance && !_measuring;
            if (overflow)
            {
                if (!clipReported) Warn(PdfHtmlWarningKind.ContentClipped, box.Element?.Name ?? "text", "A line of text is wider than the body; it is clipped at the body edge.");
                clipReported = true;
                fragment.Commands.Add(new ClipBeginCommand(0, _y - 1, _width, height + 2));
            }
            fragment.Commands.AddRange(backgrounds);
            fragment.Commands.AddRange(texts);
            if (overflow) fragment.Commands.Add(new ClipEndCommand());
        }
    }

    private void AddLink(InlineItem item, double x, double bottom, double width, double height, List<HtmlCommand> commands)
    {
        if (item.Link is not { } href || width <= 0) return;
        string? uri = Classify(href, out string? target);
        if (uri is null && target is null)
        {
            if (!_measuring && _reportedLinks.Add(href))
                Warn(PdfHtmlWarningKind.LinkDropped, href, $"The link \"{href}\" is not http, https, mailto, or #id; its text is kept.");
            return;
        }
        // Adjacent pieces of one link on a line become one rectangle.
        if (commands.LastOrDefault(c => c is LinkCommand) is LinkCommand previous && previous.Uri == uri && previous.TargetId == target &&
            Math.Abs(previous.X + previous.Width - x) < 0.01 && Math.Abs(previous.Bottom - bottom) < height)
        {
            int index = commands.LastIndexOf(previous);
            double newBottom = Math.Min(previous.Bottom, bottom);
            double newTop = Math.Max(previous.Bottom + previous.Height, bottom + height);
            commands[index] = previous with { Width = x + width - previous.X, Bottom = newBottom, Height = newTop - newBottom };
            return;
        }
        commands.Add(new LinkCommand(x, bottom, width, height, uri, target));
    }

    private (double Above, double Below) Extent(Token token, ComputedStyle block)
    {
        var style = token.Item.Style;
        if (token.Kind == TokenKind.Atomic)
        {
            double h = token.Height;
            if (style.VerticalAlign == CssVerticalAlign.Middle) return (h / 2 + block.FontSize * 0.3, h / 2 - block.FontSize * 0.3);
            return (h, 0);
        }
        double shift = Shift(style, block);
        double above = StrutAbove(style) + shift;
        return (above, style.LineHeight - StrutAbove(style) - shift);
    }

    private static StandardFont Font(ComputedStyle style) => StandardFonts.Select(style.FontFamily, style.FontWeight, style.Italic);

    private void PlaceAtomic(Token token, double x, double baseline, ComputedStyle block, List<HtmlCommand> commands)
    {
        double bottom = token.Item.Style.VerticalAlign == CssVerticalAlign.Middle
            ? baseline + block.FontSize * 0.3 - token.Height / 2
            : baseline;
        switch (((AtomicItem)token.Item).Box)
        {
            case HtmlImageBox image:
                commands.Add(new ImageCommand(image.Image, x, bottom, token.Width, token.Height));
                break;
            case BlockBox inlineBlock when token.Inner is { } inner:
                double dy = bottom + token.Height;
                foreach (var command in inner) commands.Add(Translate(command, x, dy));
                _ = inlineBlock;
                break;
        }
    }

    // How far sub and super move the baseline, from the block's font size.
    private static double Shift(ComputedStyle style, ComputedStyle block) => style.VerticalAlign switch
    {
        CssVerticalAlign.Sub => -0.2 * block.FontSize,
        CssVerticalAlign.Super => 0.35 * block.FontSize,
        _ => 0
    };

    private static IEnumerable<Token> SplitWord(Token token, double firstWidth, double width)
    {
        var style = token.Item.Style;
        var font = Font(style);
        var sb = new StringBuilder();
        double available = firstWidth;
        double current = 0;
        foreach (char c in token.Text)
        {
            double w = StandardFontMetrics.Measure(font, c.ToString(), style.FontSize) + style.LetterSpacing;
            if (sb.Length > 0 && current + w > available + Tolerance)
            {
                yield return token with { Text = sb.ToString(), Width = current, Starts = sb.Length == token.Text.Length ? token.Starts : new List<HtmlElement>() };
                sb.Clear();
                current = 0;
                available = width;
            }
            sb.Append(c);
            current += w;
        }
        if (sb.Length > 0) yield return token with { Text = sb.ToString(), Width = current, Starts = new List<HtmlElement>() };
    }

    private static double StrutAbove(ComputedStyle style)
    {
        double size = style.FontSize;
        return Ascent * size + (style.LineHeight - size) / 2;
    }

    private List<Token> Tokenize(BlockBox box, double width)
    {
        var tokens = new List<Token>();
        bool lastWasSpace = true;
        foreach (var item in box.Inlines)
        {
            var style = item.Style;
            switch (item)
            {
                case BreakItem:
                    tokens.Add(new Token(TokenKind.Break, string.Empty, item, 0, false, item.Starts));
                    lastWasSpace = true;
                    continue;
                case AtomicItem atomic:
                    tokens.Add(AtomicToken(atomic, width));
                    lastWasSpace = false;
                    continue;
            }

            var text = (TextItem)item;
            string value = ApplyTransform(text.Text, style.TextTransform);
            var font = Font(style);
            double Measure(string s) => StandardFontMetrics.Measure(font, s, style.FontSize) + style.LetterSpacing * s.Length;
            var starts = item.Starts;
            bool startsUsed = false;
            List<HtmlElement> TakeStarts()
            {
                if (startsUsed) return new List<HtmlElement>();
                startsUsed = true;
                return starts;
            }

            if (value.Length == 0)
            {
                tokens.Add(new Token(TokenKind.Word, string.Empty, item, 0, false, TakeStarts()));
                continue;
            }

            bool preserveSpaces = style.WhiteSpace is CssWhiteSpace.Pre or CssWhiteSpace.PreWrap;
            bool preserveNewlines = preserveSpaces || style.WhiteSpace == CssWhiteSpace.PreLine;
            var word = new StringBuilder();
            void EndWord()
            {
                if (word.Length == 0) return;
                string w = word.ToString();
                tokens.Add(new Token(TokenKind.Word, w, item, Measure(w), false, TakeStarts()));
                word.Clear();
                lastWasSpace = false;
            }

            int column = 0;
            foreach (char raw in value)
            {
                char c = raw == '\r' ? '\n' : raw;
                if (c == '\n' && preserveNewlines)
                {
                    EndWord();
                    tokens.Add(new Token(TokenKind.Break, string.Empty, item, 0, false, TakeStarts()));
                    lastWasSpace = true;
                    column = 0;
                    continue;
                }
                if (c is ' ' or '\t' or '\n' or '\f')
                {
                    if (preserveSpaces)
                    {
                        int count = c == '\t' ? 8 - column % 8 : 1;
                        if (style.WhiteSpace == CssWhiteSpace.Pre)
                        {
                            word.Append(' ', count);
                        }
                        else
                        {
                            EndWord();
                            string spaces = new string(' ', count);
                            tokens.Add(new Token(TokenKind.Space, spaces, item, Measure(spaces), false, TakeStarts()));
                        }
                        column += count;
                        continue;
                    }
                    EndWord();
                    if (!lastWasSpace) tokens.Add(new Token(TokenKind.Space, " ", item, Measure(" "), true, TakeStarts()));
                    lastWasSpace = true;
                    column++;
                    continue;
                }
                word.Append(c);
                column++;
            }
            EndWord();
        }
        return tokens;
    }

    private Token AtomicToken(AtomicItem atomic, double width)
    {
        switch (atomic.Box)
        {
            case HtmlImageBox image:
                var (w, h) = ImageSize(image, width);
                return new Token(TokenKind.Atomic, string.Empty, atomic, w, false, atomic.Starts) { Height = h };
            case BlockBox block:
                var s = block.Style;
                double innerWidth = s.Width is { } sw ? Resolve(sw, width) : Math.Min(IntrinsicWidths(block).Max, width);
                innerWidth = Math.Min(innerWidth, width);
                var engine = Unbounded(_model, innerWidth);
                engine.LayoutBox(block, 0, innerWidth);
                engine.Flush(0);
                double height = -engine._y;
                var (ml, mr, bl, br, pl, pr, cw) = Horizontal(s, innerWidth);
                double outer = s.Width is null ? innerWidth : ml + bl + pl + cw + pr + br + mr;
                var commands = engine._fragments.TryGetValue(0, out var fragment) ? fragment.Commands : new List<HtmlCommand>();
                return new Token(TokenKind.Atomic, string.Empty, atomic, Math.Min(outer, width), false, atomic.Starts) { Height = height, Inner = commands };
            default:
                return new Token(TokenKind.Atomic, string.Empty, atomic, 0, false, atomic.Starts);
        }
    }

    // Moves a command laid out with its top at 0 and its left at 0.
    private static HtmlCommand Translate(HtmlCommand command, double dx, double dy) => command switch
    {
        FillCommand f => f with { X = f.X + dx, Bottom = f.Bottom + dy },
        CircleCommand c => c with { CenterX = c.CenterX + dx, CenterY = c.CenterY + dy },
        LineCommand l => l with { X1 = l.X1 + dx, X2 = l.X2 + dx, Y1 = l.Y1 + dy, Y2 = l.Y2 + dy },
        TextCommand t => t with { X = t.X + dx, Baseline = t.Baseline + dy },
        ImageCommand i => i with { X = i.X + dx, Bottom = i.Bottom + dy },
        ClipBeginCommand c => c with { X = c.X + dx, Bottom = c.Bottom + dy },
        LinkCommand k => k with { X = k.X + dx, Bottom = k.Bottom + dy },
        MarkCommand m => m with { X = m.X + dx, Y = m.Y + dy },
        _ => command
    };

    #endregion

    #endregion

    #region Nested Types

    private enum TokenKind
    {
        Word,
        Space,
        Break,
        Atomic
    }

    private sealed record Token(TokenKind Kind, string Text, InlineItem Item, double Width, bool Collapsible, List<HtmlElement> Starts)
    {
        public double Height { get; init; }

        public List<HtmlCommand>? Inner { get; init; }
    }

    private sealed class Line
    {
        public bool Forced { get; set; }

        public List<Token> Tokens { get; } = new();

        public double Width { get; set; }
    }

    #endregion
}
