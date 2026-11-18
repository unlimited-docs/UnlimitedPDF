namespace UnlimitedPDF.Models.Internal.Importing;

/// <summary>
/// Keeps only the resources a page's content uses, so extracting pages does not carry every page's fonts and images
/// (WR-040). A page's resource dictionary becomes a direct copy holding the names its content, the content annotations
/// add, and the forms, tiling patterns, and Type 3 glyphs it draws without resources of their own use. A form, tiling
/// pattern, Type 3 font, or annotation appearance with its own resources gets a trimmed copy of itself, returned in
/// <see cref="Overrides"/>. Content that cannot be read (a filter other than <c>FlateDecode</c>, damaged data, data
/// over the file's stream limit, an inline image, or nesting deeper than <see cref="PdfParser.MaxDepth"/>) keeps the
/// dictionary it draws with whole, so the output is always complete (A06, A09).
/// </summary>
internal sealed class ResourceTrimmer
{
    #region Fields and Constants

    private static readonly HashSet<string> Categories = new(StringComparer.Ordinal)
    {
        "ColorSpace", "ExtGState", "Font", "Pattern", "Properties", "Shading", "XObject",
    };

    // Color spaces content uses by family name rather than by resource name.
    private static readonly string[] DefaultColorSpaces = { "DefaultCMYK", "DefaultGray", "DefaultRGB" };

    private readonly HashSet<int> _inProgress = new();
    private readonly long _maxStreamSize;
    private readonly Dictionary<int, PdfReadValue> _overrides = new();
    private readonly HashSet<int> _settled = new();
    private readonly PdfObjectTable _table;

    #endregion

    #region Constructors and Finalizers

    /// <summary>
    /// Creates a trimmer for one file's objects.
    /// </summary>
    /// <param name="table">The file's objects; their load limits bound every decode.</param>
    public ResourceTrimmer(PdfObjectTable table)
    {
        _table = table;
        _maxStreamSize = table.Limits.MaxStreamSize;
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns the resources a page keeps: a direct dictionary holding only the names its content uses, or
    /// <paramref name="resources"/> unchanged when its content cannot be read. Forms, tiling patterns, Type 3 fonts,
    /// and appearances of <paramref name="annotations"/> that have resources of their own are trimmed into
    /// <see cref="Overrides"/>.
    /// </summary>
    /// <param name="resources">The page's resources, inherited ones resolved; null when it has none.</param>
    /// <param name="contents">The page's <c>/Contents</c>: one stream, an array of streams, or null.</param>
    /// <param name="addedContents">Content the import adds to the page, not encoded.</param>
    /// <param name="annotations">The annotations the page keeps, resolved.</param>
    /// <returns>The resources to copy.</returns>
    public PdfReadValue? TrimPage(PdfReadValue? resources, PdfReadValue? contents, IEnumerable<byte[]> addedContents, IEnumerable<PdfReadDictionary> annotations)
    {
        foreach (var annotation in annotations) TrimAppearances(annotation);

        if (_table.Resolve(resources) is not PdfReadDictionary dictionary) return resources;
        var used = new Usage();
        var scanning = new HashSet<int>();

        // The streams of /Contents form one content stream: an operator may follow operands from the stream before
        // it, so they are read as one sequence, joined by white space (review R-01).
        bool readable = TryDecodeAll(Streams(contents), out var content) && ScanData(content, dictionary, used, 0, scanning);
        foreach (var data in addedContents)
            readable = readable && ScanData(data, dictionary, used, 0, scanning);
        return readable ? Filter(dictionary, used) : resources;
    }

    #endregion

    #region Private Methods

    // A direct copy of a resource dictionary keeping only the used names of each category; other entries (ProcSet)
    // are kept as they are, and a category left empty is dropped.
    private PdfReadDictionary Filter(PdfReadDictionary resources, Usage used)
    {
        var trimmed = new PdfReadDictionary();
        foreach (var key in resources.Keys)
        {
            if (!Categories.Contains(key))
            {
                trimmed.Set(key, resources[key]!);
                continue;
            }
            if (_table.Resolve(resources[key]) is not PdfReadDictionary category) continue;

            var kept = new PdfReadDictionary();
            foreach (var name in category.Keys)
            {
                if (used.Contains(key, name) || (key == "ColorSpace" && Array.IndexOf(DefaultColorSpaces, name) >= 0))
                    kept.Set(name, category[name]!);
            }
            if (kept.Count > 0) trimmed.Set(key, kept);
        }
        return trimmed;
    }

    // Draws a named object the content uses: a form, tiling pattern, or Type 3 font without resources of its own
    // draws with the parent's, so its names count for the parent; one with its own resources is trimmed on its own.
    private bool Follow(string category, string name, PdfReadDictionary resources, Usage used, int depth, HashSet<int> scanning)
    {
        if (category is not ("XObject" or "Pattern" or "Font")) return true;
        if (_table.Resolve(resources[category]) is not PdfReadDictionary entries) return true;
        var reference = entries[name] as PdfReadReference;
        var target = _table.Resolve(entries[name]);

        if (category == "Font")
        {
            if (target is not PdfReadDictionary font || (font["Subtype"] as PdfReadName)?.Value != "Type3") return true;
            if (_table.Resolve(font["Resources"]) is PdfReadDictionary && reference is not null)
            {
                TrimType3(reference.ObjectNumber, font, depth + 1);
                return true;
            }
            return ScanGlyphs(font, resources, used, depth + 1, scanning);
        }

        if (target is not PdfReadStream stream) return true;
        bool drawable = category == "XObject"
            ? (stream.Dictionary["Subtype"] as PdfReadName)?.Value == "Form"
            : _table.Resolve(stream.Dictionary["PatternType"]) is PdfReadNumber { IntegerValue: 1 };
        if (!drawable) return true;

        if (_table.Resolve(stream.Dictionary["Resources"]) is PdfReadDictionary && reference is not null)
        {
            TrimStream(reference.ObjectNumber, stream, depth + 1);
            return true;
        }
        if (reference is not null && !scanning.Add(reference.ObjectNumber)) return true;
        return ScanStream(stream, resources, used, depth + 1, scanning);
    }

    // Decodes and scans content drawn with resources, adding the names it uses; false when it cannot be read.
    private bool ScanData(byte[] data, PdfReadDictionary resources, Usage used, int depth, HashSet<int> scanning)
    {
        if (depth > PdfParser.MaxDepth) return false;
        List<(string Category, string Name)> uses;
        try
        {
            uses = ContentNameScanner.Scan(data);
        }
        catch (PdfReadException)
        {
            return false;
        }

        foreach (var (category, name) in uses)
        {
            if (!used.Add(category, name)) continue;
            if (!Follow(category, name, resources, used, depth, scanning)) return false;
        }
        return true;
    }

    // The glyph procedures of a Type 3 font drawn with resources.
    private bool ScanGlyphs(PdfReadDictionary font, PdfReadDictionary resources, Usage used, int depth, HashSet<int> scanning)
    {
        if (_table.Resolve(font["CharProcs"]) is not PdfReadDictionary procedures) return true;
        foreach (var key in procedures.Keys)
        {
            if (procedures[key] is PdfReadReference reference && !scanning.Add(reference.ObjectNumber)) continue;
            if (_table.Resolve(procedures[key]) is PdfReadStream glyph && !ScanStream(glyph, resources, used, depth, scanning)) return false;
        }
        return true;
    }

    private bool ScanStream(PdfReadStream stream, PdfReadDictionary resources, Usage used, int depth, HashSet<int> scanning) =>
        TryDecodeAll(new[] { stream }, out var data) && ScanData(data, resources, used, depth, scanning);

    // The streams of a /Contents value.
    private IEnumerable<PdfReadStream> Streams(PdfReadValue? contents)
    {
        switch (_table.Resolve(contents))
        {
            case PdfReadStream stream:
                yield return stream;
                break;
            case PdfReadArray array:
                foreach (var item in array.Items)
                {
                    if (_table.Resolve(item) is PdfReadStream part) yield return part;
                }
                break;
        }
    }

    // A form appearance with resources of its own.
    private void TrimAppearance(PdfReadValue? value)
    {
        if (value is PdfReadReference reference && _table.Resolve(reference) is PdfReadStream stream &&
            _table.Resolve(stream.Dictionary["Resources"]) is PdfReadDictionary)
            TrimStream(reference.ObjectNumber, stream, 1);
    }

    // Trims every appearance stream of an annotation: /N, /R, and /D, each a stream or a dictionary of states.
    private void TrimAppearances(PdfReadDictionary annotation)
    {
        if (_table.Resolve(annotation["AP"]) is not PdfReadDictionary appearances) return;
        foreach (var kind in appearances.Keys)
        {
            var value = appearances[kind];
            if (_table.Resolve(value) is PdfReadDictionary states)
            {
                foreach (var state in states.Keys) TrimAppearance(states[state]);
            }
            else
            {
                TrimAppearance(value);
            }
        }
    }

    // A form or tiling pattern with its own resources: a copy with only the resources its content uses, unless that
    // content cannot be read. Each object is settled once per trimmer; a form that draws itself is left as it is.
    private void TrimStream(int number, PdfReadStream stream, int depth)
    {
        if (_settled.Contains(number) || !_inProgress.Add(number)) return;
        var resources = (PdfReadDictionary)_table.Resolve(stream.Dictionary["Resources"]);
        var used = new Usage();
        if (ScanStream(stream, resources, used, depth, new HashSet<int> { number }))
        {
            var dictionary = new PdfReadDictionary();
            foreach (var key in stream.Dictionary.Keys)
                dictionary.Set(key, key == "Resources" ? Filter(resources, used) : stream.Dictionary[key]!);
            _overrides[number] = new PdfReadStream(dictionary, stream.Data);
        }
        _inProgress.Remove(number);
        _settled.Add(number);
    }

    // A Type 3 font with its own resources: a copy with only the resources its glyphs use.
    private void TrimType3(int number, PdfReadDictionary font, int depth)
    {
        if (_settled.Contains(number) || !_inProgress.Add(number)) return;
        var resources = (PdfReadDictionary)_table.Resolve(font["Resources"]);
        var used = new Usage();
        if (ScanGlyphs(font, resources, used, depth, new HashSet<int>()))
        {
            var copy = new PdfReadDictionary();
            foreach (var key in font.Keys) copy.Set(key, key == "Resources" ? Filter(resources, used) : font[key]!);
            _overrides[number] = copy;
        }
        _inProgress.Remove(number);
        _settled.Add(number);
    }

    // Decodes streams into one sequence, a newline between them, within the file's stream limit in all; false when a
    // stream uses a filter the reader does not decode, is damaged, or the total passes the limit.
    private bool TryDecodeAll(IEnumerable<PdfReadStream> streams, out byte[] content)
    {
        content = Array.Empty<byte>();
        using var all = new MemoryStream();
        long remaining = _maxStreamSize;
        foreach (var stream in streams)
        {
            byte[] data;
            try
            {
                data = PdfStreamDecoder.Decode(stream, _table.Resolve, remaining);
            }
            catch (PdfReadException)
            {
                // A filter the reader does not decode, damaged data, or data over the stream limit: the caller keeps
                // the whole dictionary (D-017 fallback, A09).
                return false;
            }
            remaining -= data.Length + 1;
            if (remaining < 0) return false;
            all.Write(data, 0, data.Length);
            all.WriteByte((byte)'\n');
        }
        content = all.ToArray();
        return true;
    }

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the trimmed copies of forms, tiling patterns, Type 3 fonts, and appearances, by source object number.
    /// </summary>
    public IReadOnlyDictionary<int, PdfReadValue> Overrides => _overrides;

    #endregion

    #region Nested Types

    // The names content uses, per resource category.
    private sealed class Usage
    {
        private readonly Dictionary<string, HashSet<string>> _names = new(StringComparer.Ordinal);

        // Records a use; false when it was recorded already.
        public bool Add(string category, string name)
        {
            if (!_names.TryGetValue(category, out var names)) _names[category] = names = new HashSet<string>(StringComparer.Ordinal);
            return names.Add(name);
        }

        public bool Contains(string category, string name) => _names.TryGetValue(category, out var names) && names.Contains(name);
    }

    #endregion
}
