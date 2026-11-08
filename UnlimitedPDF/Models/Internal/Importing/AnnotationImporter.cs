namespace UnlimitedPDF.Models.Internal.Importing;

/// <summary>
/// Decides what of a page's annotations survives import (WR-036, A05): links whose target page is imported in the
/// same call (named destinations resolved), <c>URI</c> actions, and comments are kept; scripts, launches, form
/// submissions, other actions, media, and attachments are removed; visible form fields are painted from their normal
/// appearance and removed, so they keep their look but are no longer editable.
/// </summary>
internal sealed class AnnotationImporter
{
    #region Fields and Constants

    /// <summary>
    /// The resource-name prefix of a painted form field.
    /// </summary>
    public const string FieldResourcePrefix = "UnlimitedField";

    // Annotation flags: Hidden (bit 2) and NoView (bit 6).
    private const int HiddenFlags = 2 | 32;

    private static readonly HashSet<string> DroppedSubtypes = new(StringComparer.Ordinal)
    {
        "Screen", "Movie", "Sound", "RichMedia", "3D", "FileAttachment",
    };

    private readonly PdfReadDictionary _catalog;
    private readonly PdfObjectTable _table;
    private Dictionary<string, PdfReadValue>? _named;

    #endregion

    #region Constructors and Finalizers

    /// <summary>
    /// Creates the importer for one file.
    /// </summary>
    /// <param name="source">The file that was read.</param>
    public AnnotationImporter(PdfSourceFile source)
    {
        _table = source.Objects;
        _catalog = source.Catalog;
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns how <paramref name="page"/>'s annotations, content, and resources are copied.
    /// </summary>
    /// <param name="page">The page.</param>
    /// <param name="imported">The source page objects imported in the same call; links to other pages are removed.</param>
    /// <param name="nextTemporary">Returns a fresh negative number for a new object this rewrite adds.</param>
    /// <returns>The rewrite.</returns>
    public PageRewrite Rewrite(PdfSourcePage page, ISet<int> imported, Func<int> nextTemporary)
    {
        var rewrite = new PageRewrite();
        if (_table.Resolve(page.Dictionary["Annots"]) is not PdfReadArray annotations) return rewrite;

        var kept = new List<PdfReadValue>();
        var fields = new List<(PdfReadReference Form, double[] Matrix)>();
        foreach (var item in annotations.Items)
        {
            if (_table.Resolve(item) is not PdfReadDictionary annotation)
            {
                Remove(item, rewrite);
                continue;
            }

            string subtype = (annotation["Subtype"] as PdfReadName)?.Value ?? "";
            PdfReadDictionary? copy = null;
            if (subtype == "Widget")
            {
                if (Appearance(annotation) is { } field) fields.Add(field);
            }
            else if (!DroppedSubtypes.Contains(subtype))
            {
                copy = RewriteAnnotation(annotation, subtype, imported);
            }

            if (copy is null)
            {
                Remove(item, rewrite);
            }
            else if (item is PdfReadReference reference)
            {
                rewrite.Overrides[reference.ObjectNumber] = copy;
                kept.Add(item);
            }
            else
            {
                kept.Add(copy);
            }
        }

        rewrite.Annots = kept.Count > 0 ? new PdfReadArray(kept) : null;
        if (fields.Count > 0) PaintFields(page, fields, rewrite, nextTemporary);
        return rewrite;
    }

    #endregion

    #region Internal Methods

    /// <summary>
    /// Returns the explicit destination <paramref name="value"/> names, resolving named destinations, when its page
    /// is one of <paramref name="imported"/>; otherwise null.
    /// </summary>
    /// <param name="value">A destination array, name, string, or dictionary with <c>/D</c>.</param>
    /// <param name="imported">The source page objects imported in the same call.</param>
    /// <returns>The destination array, or null.</returns>
    internal PdfReadArray? Destination(PdfReadValue? value, ISet<int> imported)
    {
        var resolved = _table.Resolve(value);
        string? name = resolved switch
        {
            PdfReadName n => n.Value,
            PdfReadString s => Encoding.Latin1.GetString(s.Bytes),
            _ => null,
        };
        if (name is not null) resolved = Named().TryGetValue(name, out var target) ? _table.Resolve(target) : PdfReadNull.Instance;
        if (resolved is PdfReadDictionary withD) resolved = _table.Resolve(withD["D"]);

        return resolved is PdfReadArray { Count: > 0 } array && array[0] is PdfReadReference page && imported.Contains(page.ObjectNumber)
            ? array
            : null;
    }

    #endregion

    #region Private Methods

    // The normal appearance of a visible form field and the matrix that maps its bounding box onto the field's
    // rectangle, or null when the field is hidden or has no appearance.
    private (PdfReadReference Form, double[] Matrix)? Appearance(PdfReadDictionary field)
    {
        long flags = (_table.Resolve(field["F"]) as PdfReadNumber)?.IntegerValue ?? 0;
        if ((flags & HiddenFlags) != 0) return null;
        if (_table.Resolve(field["AP"]) is not PdfReadDictionary appearances) return null;

        var normal = appearances["N"];
        if (_table.Resolve(normal) is PdfReadDictionary states && _table.Resolve(field["AS"]) is PdfReadName state)
            normal = states[state.Value];
        if (normal is not PdfReadReference form || _table.Resolve(form) is not PdfReadStream stream) return null;

        var rect = Numbers(field["Rect"], 4);
        var box = Numbers(stream.Dictionary["BBox"], 4);
        if (rect is null || box is null) return null;
        var matrix = Numbers(stream.Dictionary["Matrix"], 6) ?? new double[] { 1, 0, 0, 1, 0, 0 };

        // The bounding box transformed by the form matrix, then scaled and moved onto the rectangle (PDF 12.5.5).
        var corners = new[] { (box[0], box[1]), (box[2], box[1]), (box[0], box[3]), (box[2], box[3]) }
            .Select(c => (X: matrix[0] * c.Item1 + matrix[2] * c.Item2 + matrix[4], Y: matrix[1] * c.Item1 + matrix[3] * c.Item2 + matrix[5]))
            .ToList();
        double minX = corners.Min(c => c.X), maxX = corners.Max(c => c.X), minY = corners.Min(c => c.Y), maxY = corners.Max(c => c.Y);
        double left = Math.Min(rect[0], rect[2]), right = Math.Max(rect[0], rect[2]);
        double bottom = Math.Min(rect[1], rect[3]), top = Math.Max(rect[1], rect[3]);
        if (maxX - minX <= 0 || maxY - minY <= 0) return null;

        double scaleX = (right - left) / (maxX - minX);
        double scaleY = (top - bottom) / (maxY - minY);
        return (form, new[] { scaleX, 0, 0, scaleY, left - minX * scaleX, bottom - minY * scaleY });
    }

    // A GoTo or URI action, with its destination resolved; null for any other action or a target page not imported.
    private PdfReadDictionary? FilterAction(PdfReadValue? value, ISet<int> imported)
    {
        if (_table.Resolve(value) is not PdfReadDictionary action) return null;
        string kind = (action["S"] as PdfReadName)?.Value ?? "";

        var copy = new PdfReadDictionary();
        copy.Set("S", new PdfReadName(kind));
        if (kind == "URI" && _table.Resolve(action["URI"]) is PdfReadString uri)
        {
            copy.Set("URI", uri);
            return copy;
        }
        if (kind == "GoTo" && Destination(action["D"], imported) is { } destination)
        {
            copy.Set("D", destination);
            return copy;
        }
        return null;
    }

    // Named destinations from the catalog's /Dests dictionary and its /Names /Dests name tree.
    private Dictionary<string, PdfReadValue> Named()
    {
        if (_named is not null) return _named;
        _named = new Dictionary<string, PdfReadValue>(StringComparer.Ordinal);
        if (_table.Resolve(_catalog["Dests"]) is PdfReadDictionary dests)
        {
            foreach (var key in dests.Keys) _named[key] = dests[key]!;
        }
        if (_table.Resolve(_catalog["Names"]) is PdfReadDictionary names)
        {
            var visited = new HashSet<int>();
            var pending = new Stack<PdfReadValue?>();
            pending.Push(names["Dests"]);
            while (pending.Count > 0)
            {
                var item = pending.Pop();
                if (item is PdfReadReference reference && !visited.Add(reference.ObjectNumber)) continue;
                if (_table.Resolve(item) is not PdfReadDictionary node) continue;
                if (_table.Resolve(node["Names"]) is PdfReadArray pairs)
                {
                    for (int i = 0; i + 1 < pairs.Count; i += 2)
                    {
                        if (_table.Resolve(pairs[i]) is PdfReadString key) _named.TryAdd(Encoding.Latin1.GetString(key.Bytes), pairs[i + 1]);
                    }
                }
                if (_table.Resolve(node["Kids"]) is PdfReadArray kids)
                {
                    foreach (var kid in kids.Items) pending.Push(kid);
                }
            }
        }
        return _named;
    }

    private double[]? Numbers(PdfReadValue? value, int count)
    {
        if (_table.Resolve(value) is not PdfReadArray array || array.Count < count) return null;
        var numbers = new double[count];
        for (int i = 0; i < count; i++)
        {
            if (_table.Resolve(array[i]) is not PdfReadNumber number) return null;
            numbers[i] = number.Value;
        }
        return numbers;
    }

    // Paints each field's appearance after the page's own content, with the content wrapped in q/Q so its graphics
    // state does not leak into the fields, and adds the appearances to a copy of the page's resources.
    private void PaintFields(PdfSourcePage page, List<(PdfReadReference Form, double[] Matrix)> fields, PageRewrite rewrite, Func<int> nextTemporary)
    {
        var draw = new StringBuilder("Q\n");
        var xObjects = new PdfReadDictionary();
        var resources = _table.Resolve(page.Inherited.TryGetValue("Resources", out var inherited) ? inherited : null) as PdfReadDictionary;
        if (_table.Resolve(resources?["XObject"]) is PdfReadDictionary existing)
        {
            foreach (var key in existing.Keys) xObjects.Set(key, existing[key]!);
        }
        for (int i = 0; i < fields.Count; i++)
        {
            string name = FieldResourcePrefix + (i + 1).ToString(CultureInfo.InvariantCulture);
            xObjects.Set(name, fields[i].Form);
            draw.Append("q ").Append(string.Join(" ", fields[i].Matrix.Select(PdfNumber.Format))).Append(" cm /").Append(name).Append(" Do Q\n");
        }

        var newResources = new PdfReadDictionary();
        foreach (var key in resources?.Keys ?? Array.Empty<string>())
        {
            if (key != "XObject") newResources.Set(key, resources![key]!);
        }
        newResources.Set("XObject", xObjects);
        rewrite.Resources = newResources;

        int open = nextTemporary();
        int close = nextTemporary();
        rewrite.NewStreams[open] = Encoding.ASCII.GetBytes("q\n");
        rewrite.NewStreams[close] = Encoding.ASCII.GetBytes(draw.ToString());

        var contents = new List<PdfReadValue> { new PdfReadReference(open, 0) };
        var original = page.Dictionary["Contents"];
        if (_table.Resolve(original) is PdfReadArray array) contents.AddRange(array.Items);
        else if (original is not null) contents.Add(original);
        contents.Add(new PdfReadReference(close, 0));
        rewrite.Contents = new PdfReadArray(contents);
    }

    private static void Remove(PdfReadValue item, PageRewrite rewrite)
    {
        if (item is PdfReadReference reference) rewrite.Removed.Add(reference.ObjectNumber);
    }

    // A copy of a kept annotation without additional actions or tagging, with its action filtered and its
    // destination resolved. A link left without a target is removed (null).
    private PdfReadDictionary? RewriteAnnotation(PdfReadDictionary annotation, string subtype, ISet<int> imported)
    {
        var copy = new PdfReadDictionary();
        bool hasTarget = false;
        foreach (var key in annotation.Keys)
        {
            switch (key)
            {
                case "AA":
                case "StructParent":
                    break;
                case "A":
                    if (FilterAction(annotation[key], imported) is { } action)
                    {
                        copy.Set(key, action);
                        hasTarget = true;
                    }
                    break;
                case "Dest":
                    if (Destination(annotation[key], imported) is { } destination)
                    {
                        copy.Set(key, destination);
                        hasTarget = true;
                    }
                    break;
                default:
                    copy.Set(key, annotation[key]!);
                    break;
            }
        }
        return subtype == "Link" && !hasTarget ? null : copy;
    }

    #endregion

    #endregion
}

/// <summary>
/// How one page is copied when its annotations change it: replaced annotation objects, removed objects, the new
/// <c>/Annots</c>, and, when form fields are painted, new <c>/Contents</c>, <c>/Resources</c>, and the content
/// streams that paint them (keyed by temporary negative numbers).
/// </summary>
internal sealed class PageRewrite
{
    #region Properties, Indexers

    /// <summary>
    /// Gets or sets the page's new <c>/Annots</c>, or null to drop the key.
    /// </summary>
    public PdfReadValue? Annots { get; set; }

    /// <summary>
    /// Gets or sets the page's new <c>/Contents</c>, or null to keep its own.
    /// </summary>
    public PdfReadValue? Contents { get; set; }

    /// <summary>
    /// Gets the content streams this rewrite adds, by temporary number.
    /// </summary>
    public Dictionary<int, byte[]> NewStreams { get; } = new();

    /// <summary>
    /// Gets the rewritten annotation objects, by source object number.
    /// </summary>
    public Dictionary<int, PdfReadDictionary> Overrides { get; } = new();

    /// <summary>
    /// Gets the source objects removed from the page.
    /// </summary>
    public HashSet<int> Removed { get; } = new();

    /// <summary>
    /// Gets or sets the page's new <c>/Resources</c>, or null to keep its own.
    /// </summary>
    public PdfReadValue? Resources { get; set; }

    #endregion
}
