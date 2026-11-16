using UnlimitedPDF.Models.Internal.Reading;

namespace UnlimitedPDF.Models.Internal.Rendering;

internal sealed partial class PageInterpreter
{
    #region Fields and Constants

    // The most annotations read on a page; the most entries and nodes read from the named-destination tree, once
    // per page; and the most form XObjects drawn on a page.
    private const int MaxAnnotations = 10_000;
    private const int MaxNameTreeEntries = 100_000;
    private const int MaxFormDraws = 10_000;

    // Annotation flags that hide an annotation: Hidden (2) and NoView (32).
    private const long HiddenFlags = 2 | 32;

    // Decoded form XObject content, so a form drawn many times is decoded once.
    private readonly Dictionary<PdfReadStream, byte[]> _formContent = new(ReferenceEqualityComparer.Instance);
    private Dictionary<string, PdfReadValue>? _namedDestinations;
    private int _formDraws;

    private static readonly string[] AllowedSchemes = { "http", "https", "mailto" };

    #endregion

    #region Methods

    #region Private Methods

    // Turns the page's link annotations into SVG links over their rectangles. Only http, https, and mailto URIs and
    // links to pages of the same document are kept; any other action is dropped (A05). A link that can't be read is
    // dropped, not the page: a link is not part of what the page shows.
    private void DrawAnnotations(PdfReadDictionary page)
    {
        if (_objects.Resolve(page["Annots"]) is not PdfReadArray annotations) return;
        foreach (var item in annotations.Items.Take(MaxAnnotations))
        {
            if (_objects.Resolve(item) is not PdfReadDictionary annotation) continue;
            if (_objects.Resolve(annotation["Subtype"]) is not PdfReadName { Value: "Link" }) continue;
            if (_objects.Resolve(annotation["F"]) is PdfReadNumber flags && (flags.IntegerValue & HiddenFlags) != 0) continue;
            if (_objects.Resolve(annotation["Rect"]) is not PdfReadArray { Count: 4 } rect) continue;
            var r = rect.Items.Select(v => _objects.Resolve(v) is PdfReadNumber n ? n.Value : double.NaN).ToArray();
            if (r.Any(v => !double.IsFinite(v) || Math.Abs(v) > SvgText.MaxMagnitude)) continue;

            string? href = null, label = null;
            if (_objects.Resolve(annotation["A"]) is PdfReadDictionary action)
            {
                string kind = (_objects.Resolve(action["S"]) as PdfReadName)?.Value ?? string.Empty;
                if (kind == "URI" && _objects.Resolve(action["URI"]) is PdfReadString uri && SafeUri(uri.Bytes) is { } safe)
                {
                    href = safe;
                    label = safe;
                }
                else if (kind == "GoTo" && TargetPage(action["D"]) is int target)
                {
                    href = $"#{_idPrefix}page-{target}";
                    label = $"Go to page {target}";
                }
            }
            else if (annotation.ContainsKey("Dest") && TargetPage(annotation["Dest"]) is int target)
            {
                href = $"#{_idPrefix}page-{target}";
                label = $"Go to page {target}";
            }
            if (href is null) continue;

            double left = Math.Min(r[0], r[2]), bottom = Math.Min(r[1], r[3]);
            double width = Math.Abs(r[2] - r[0]), height = Math.Abs(r[3] - r[1]);
            _body.Append("<a href=\"");
            SvgText.AppendEscaped(_body, href);
            _body.Append("\" aria-label=\"");
            SvgText.AppendEscaped(_body, label!);
            _body.Append('"');
            if (!href.StartsWith("#", StringComparison.Ordinal)) _body.Append(" target=\"_blank\" rel=\"noopener noreferrer\"");
            else _body.Append(" data-pdf-page=\"").Append(href.Substring(href.LastIndexOf('-') + 1)).Append('"');
            _body.Append("><title>");
            SvgText.AppendEscaped(_body, label!);
            _body.Append("</title><path d=\"M").Append(SvgText.Number(left)).Append(' ').Append(SvgText.Number(bottom))
                .Append('h').Append(SvgText.Number(width)).Append('v').Append(SvgText.Number(height))
                .Append('h').Append(SvgText.Number(-width)).Append("Z\" transform=\"");
            SvgText.AppendMatrix(_body, _pageMatrix);
            _body.Append("\" fill=\"#ffffff\" fill-opacity=\"0\"/></a>");
            _links++;
            CheckOutput();
        }
    }

    // Draws a form XObject: its content, through its /Matrix and clipped to its /BBox, with its own resources.
    private void DrawForm(PdfReadStream form)
    {
        var dictionary = form.Dictionary;
        if (_inText) throw new PageRenderException("The page draws a form XObject inside a text object, which PDF doesn't allow.");
        if (_formDepth >= _limits.MaxFormDepth) throw new PageRenderException("The page nests form XObjects too deeply.");
        if (++_formDraws > MaxFormDraws) throw new PageRenderException("The page draws too many form XObjects.");
        if (dictionary.ContainsKey("Group") || dictionary.ContainsKey("OC"))
            throw new PageRenderException("The page draws a form XObject with a transparency group or optional content, which this preview doesn't draw.");
        if (_objects.Resolve(dictionary["BBox"]) is not PdfReadArray { Count: 4 } boxArray)
            throw new PageRenderException("A form XObject on the page has no bounding box.");
        var box = boxArray.Items.Select(v => _objects.Resolve(v) is PdfReadNumber n && double.IsFinite(n.Value) ? n.Value : throw new PageRenderException("A form XObject's bounding box is not valid.")).ToArray();
        var matrix = RenderMatrix.Identity;
        if (_objects.Resolve(dictionary["Matrix"]) is PdfReadArray { Count: 6 } m)
        {
            var v = m.Items.Select(item => _objects.Resolve(item) is PdfReadNumber n && double.IsFinite(n.Value) ? n.Value : throw new PageRenderException("A form XObject's matrix is not valid.")).ToArray();
            matrix = new RenderMatrix(v[0], v[1], v[2], v[3], v[4], v[5]);
        }

        if (_saved.Count >= _limits.MaxStateDepth) throw new PageRenderException("The page nests saved states too deeply.");
        _saved.Push(_state);
        _state = _state.Copy();
        _state.OpenGroups = 0;
        _state.Ctm = matrix.Then(_state.Ctm);
        if (!_state.Ctm.IsFinite()) throw new PageRenderException("The page has a transformation that is not finite.");

        // Clip to the bounding box, as a path the form never painted.
        AddSegment("M", Math.Min(box[0], box[2]), Math.Min(box[1], box[3]));
        AddSegment("L", Math.Max(box[0], box[2]), Math.Min(box[1], box[3]));
        AddSegment("L", Math.Max(box[0], box[2]), Math.Max(box[1], box[3]));
        AddSegment("L", Math.Min(box[0], box[2]), Math.Max(box[1], box[3]));
        AddSegment("Z");
        _pendingClip = "W";
        Paint(false, false, false);

        var resources = _objects.Resolve(dictionary["Resources"]) as PdfReadDictionary ?? _currentResources;
        var outer = _currentResources;
        if (!_formContent.TryGetValue(form, out var content))
        {
            content = Decode(form);
            _formContent[form] = content;
        }
        _formDepth++;
        try
        {
            Run(content, resources);
        }
        finally
        {
            _formDepth--;
            _currentResources = outer;
        }
        if (_inText) throw new PageRenderException("A form XObject on the page leaves a text object open.");
        if (_pathHasPoints) throw new PageRenderException("A form XObject on the page leaves a path unfinished.");
        Restore();
    }

    // The 1-based number of the page a destination points to, or null.
    private int? TargetPage(PdfReadValue? value)
    {
        var resolved = _objects.Resolve(value);
        string? name = resolved switch
        {
            PdfReadName n => n.Value,
            PdfReadString s => Encoding.Latin1.GetString(s.Bytes),
            _ => null,
        };
        if (name is not null) resolved = _objects.Resolve(NamedDestination(name));
        if (resolved is PdfReadDictionary withD) resolved = _objects.Resolve(withD["D"]);
        if (resolved is not PdfReadArray { Count: > 0 } array || array[0] is not PdfReadReference page) return null;
        int index = _pageObjects.IndexOf(page.ObjectNumber);
        return index < 0 ? null : index + 1;
    }

    // Looks a name up in the catalog's /Dests dictionary, then in its /Names /Dests name tree.
    private PdfReadValue? NamedDestination(string name)
    {
        if (_objects.Resolve(_catalog["Dests"]) is PdfReadDictionary dests && dests[name] is { } direct) return direct;
        _namedDestinations ??= ReadNameTree();
        return _namedDestinations.TryGetValue(name, out var target) ? target : null;
    }

    // Reads the /Names /Dests name tree once: each node at most once, and a bounded number of entries and kids in all,
    // so a tree that lists itself or is huge costs no more than the bound (A06).
    private Dictionary<string, PdfReadValue> ReadNameTree()
    {
        var map = new Dictionary<string, PdfReadValue>(StringComparer.Ordinal);
        if (_objects.Resolve(_catalog["Names"]) is not PdfReadDictionary names || _objects.Resolve(names["Dests"]) is not PdfReadDictionary root) return map;

        var visited = new HashSet<PdfReadDictionary>(ReferenceEqualityComparer.Instance);
        var pending = new Stack<PdfReadDictionary>();
        pending.Push(root);
        int read = 0;
        while (pending.Count > 0)
        {
            var node = pending.Pop();
            if (!visited.Add(node)) continue;
            if (_objects.Resolve(node["Names"]) is PdfReadArray pairs)
            {
                for (int i = 0; i + 1 < pairs.Count; i += 2)
                {
                    if (++read > MaxNameTreeEntries) return map;
                    if (_objects.Resolve(pairs[i]) is PdfReadString key) map.TryAdd(Encoding.Latin1.GetString(key.Bytes), pairs[i + 1]);
                }
            }
            if (_objects.Resolve(node["Kids"]) is PdfReadArray kids)
            {
                foreach (var kid in kids.Items)
                {
                    if (++read > MaxNameTreeEntries) return map;
                    if (_objects.Resolve(kid) is PdfReadDictionary child && !visited.Contains(child)) pending.Push(child);
                }
            }
        }
        return map;
    }

    // The URI as text when it is an absolute http, https, or mailto URI without control characters or spaces.
    private static string? SafeUri(byte[] bytes)
    {
        if (bytes.Length == 0 || bytes.Length > 2048 || bytes.Any(b => b <= 0x20 || b >= 0x7F)) return null;
        string text = Encoding.ASCII.GetString(bytes);
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri)) return null;
        if (!AllowedSchemes.Contains(uri.Scheme, StringComparer.OrdinalIgnoreCase)) return null;

        // A mail link needs an address.
        if (uri.Scheme.Equals("mailto", StringComparison.OrdinalIgnoreCase) && !text.Substring("mailto:".Length).Split('?')[0].Contains('@')) return null;
        return text;
    }

    #endregion

    #endregion
}
