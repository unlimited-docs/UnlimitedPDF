using UnlimitedPDF.Models.Internal.Reading;

namespace UnlimitedPDF.Models.Internal.Rendering;

/// <summary>
/// Runs a page's content operators and writes what they draw as SVG. It understands the operators the library writes
/// and a few close relatives; anything else stops the page with a <see cref="PageRenderException"/>, so a page is
/// either drawn completely or not at all.
/// </summary>
internal sealed partial class PageInterpreter
{
    #region Fields and Constants

    private readonly StringBuilder _body = new();
    private readonly StringBuilder _defs = new();
    private readonly string _idPrefix;
    private readonly PageRenderLimits _limits;
    private readonly StringBuilder _path = new();
    private readonly PdfObjectTable _objects;
    private readonly Stack<RenderState> _saved = new();

    private long _contentBytes;
    private (double X, double Y) _current;
    private (double X, double Y) _subpathStart;
    private int _nextId;
    private int _operators;
    private bool _pathHasPoints;
    private int _pathSegments;
    private int _paths;
    private string? _pendingClip;
    private RenderState _state = new();

    #endregion

    #region Constructors and Finalizers

    /// <summary>
    /// Creates an interpreter for one page.
    /// </summary>
    /// <param name="objects">The file's objects, to resolve references.</param>
    /// <param name="limits">The work bounds.</param>
    /// <param name="idPrefix">A prefix for element ids, unique among the drawings shown together.</param>
    public PageInterpreter(PdfObjectTable objects, PageRenderLimits limits, string idPrefix)
    {
        _objects = objects;
        _limits = limits;
        _idPrefix = idPrefix;
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Draws a page.
    /// </summary>
    /// <param name="page">The page dictionary.</param>
    /// <param name="inherited">The page's own or inherited resources, media box, crop box, and rotation.</param>
    /// <param name="pageNumber">The 1-based page number.</param>
    /// <returns>The drawn page.</returns>
    /// <exception cref="PageRenderException">The page holds content the renderer does not draw.</exception>
    public PageRenderResult Draw(PdfReadDictionary page, IReadOnlyDictionary<string, PdfReadValue> inherited, int pageNumber)
    {
        var box = Box(inherited.TryGetValue("CropBox", out var crop) ? crop : null)
            ?? Box(inherited.TryGetValue("MediaBox", out var media) ? media : null)
            ?? throw new PageRenderException("The page has no media box.");
        int rotate = inherited.TryGetValue("Rotate", out var rotateValue) && _objects.Resolve(rotateValue) is PdfReadNumber r
            ? (int)(((r.IntegerValue % 360) + 360) % 360)
            : 0;
        if (rotate % 90 != 0) throw new PageRenderException("The page has a rotation that is not a multiple of 90 degrees.");

        double width = box.X1 - box.X0, height = box.Y1 - box.Y0;
        if (width <= 0 || height <= 0) throw new PageRenderException("The page has an empty media box.");

        // From PDF user space (y up, origin at the box corner) to SVG (y down), turned by the page rotation.
        var flip = new RenderMatrix(1, 0, 0, -1, -box.X0, box.Y1);
        var turn = rotate switch
        {
            90 => new RenderMatrix(0, 1, -1, 0, height, 0),
            180 => new RenderMatrix(-1, 0, 0, -1, width, height),
            270 => new RenderMatrix(0, -1, 1, 0, 0, width),
            _ => RenderMatrix.Identity,
        };
        _state.Ctm = flip.Then(turn);
        double shownWidth = rotate is 90 or 270 ? height : width;
        double shownHeight = rotate is 90 or 270 ? width : height;

        var resources = _objects.Resolve(inherited.TryGetValue("Resources", out var res) ? res : null) as PdfReadDictionary;
        foreach (var stream in ContentStreams(page)) Run(stream, resources);
        while (_saved.Count > 0) Restore();
        CloseGroups(_state);
        DrawAnnotations(page, pageNumber);

        var svg = new StringBuilder();
        svg.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 ").Append(SvgText.Number(shownWidth)).Append(' ')
            .Append(SvgText.Number(shownHeight)).Append("\" width=\"").Append(SvgText.Number(shownWidth))
            .Append("\" height=\"").Append(SvgText.Number(shownHeight)).Append("\">");
        if (_defs.Length > 0) svg.Append("<defs>").Append(_defs).Append("</defs>");
        svg.Append("<rect width=\"100%\" height=\"100%\" fill=\"#ffffff\"/>");
        svg.Append(_body).Append("</svg>");
        if (svg.Length > _limits.MaxOutputCharacters) throw new PageRenderException("The page drawing is too large.");

        return new PageRenderResult(pageNumber, shownWidth, shownHeight, svg.ToString(), null, TextLines,
            new PageRenderCounts(_paths, _glyphs, _images, _links));
    }

    #endregion

    #region Private Methods

    private static double Number(PdfReadValue value) => value is PdfReadNumber number
        ? number.Value
        : throw new PageRenderException("An operator has an operand that is not a number.");

    private static double[] Numbers(IReadOnlyList<PdfReadValue> operands, int count)
    {
        if (operands.Count < count) throw new PageRenderException("An operator has too few operands.");
        var values = new double[count];
        for (int i = 0; i < count; i++)
        {
            values[i] = Number(operands[operands.Count - count + i]);
            if (!double.IsFinite(values[i])) throw new PageRenderException("An operator has an operand that is not finite.");
        }
        return values;
    }

    private void AddSegment(string command, params double[] points)
    {
        if (++_pathSegments > _limits.MaxPathSegments) throw new PageRenderException("The page has too many path segments.");
        _path.Append(command);
        for (int i = 0; i < points.Length; i++) _path.Append(i == 0 ? "" : " ").Append(SvgText.Number(points[i]));
        _pathHasPoints = true;
        if (command == "Z")
        {
            _current = _subpathStart;
        }
        else
        {
            _current = (points[^2], points[^1]);
            if (command == "M") _subpathStart = _current;
        }
    }

    private (double X0, double Y0, double X1, double Y1)? Box(PdfReadValue? value)
    {
        if (_objects.Resolve(value) is not PdfReadArray { Count: 4 } array) return null;
        var n = array.Items.Select(item => _objects.Resolve(item) is PdfReadNumber number ? number.Value : double.NaN).ToArray();
        if (n.Any(v => !double.IsFinite(v))) return null;
        return (Math.Min(n[0], n[2]), Math.Min(n[1], n[3]), Math.Max(n[0], n[2]), Math.Max(n[1], n[3]));
    }

    private void CloseGroups(RenderState state)
    {
        for (int i = 0; i < state.OpenGroups; i++) _body.Append("</g>");
        state.OpenGroups = 0;
    }

    private IEnumerable<byte[]> ContentStreams(PdfReadDictionary page)
    {
        var contents = _objects.Resolve(page["Contents"]);
        var streams = contents switch
        {
            PdfReadStream stream => new List<PdfReadValue> { stream },
            PdfReadArray array => array.Items.Select(item => _objects.Resolve(item)).ToList(),
            _ => new List<PdfReadValue>(),
        };
        foreach (var item in streams)
        {
            if (item is not PdfReadStream stream) throw new PageRenderException("The page content is not a stream.");
            yield return Decode(stream);
        }
    }

    private byte[] Decode(PdfReadStream stream)
    {
        byte[] data;
        try
        {
            data = PdfStreamDecoder.Decode(stream, _objects.Resolve, _limits.MaxContentBytes - _contentBytes);
        }
        catch (PdfReadException exception)
        {
            throw new PageRenderException($"A stream on the page can't be read: {exception.Message}");
        }
        _contentBytes += data.Length;
        if (_contentBytes > _limits.MaxContentBytes) throw new PageRenderException("The page content is too large.");
        return data;
    }

    private string NewId(string kind) => $"{_idPrefix}{kind}{_nextId++}";

    // Ends the current path: fills or strokes it, then applies a pending clip (W or W*), then clears it.
    private void Paint(bool fill, bool stroke, bool evenOdd)
    {
        if (_pathHasPoints && (fill || stroke))
        {
            _paths++;
            _body.Append("<path d=\"").Append(_path).Append("\" transform=\"");
            SvgText.AppendMatrix(_body, _state.Ctm);
            _body.Append('"');
            if (fill)
            {
                _body.Append(" fill=\"");
                SvgText.AppendColor(_body, _state.Fill.Red, _state.Fill.Green, _state.Fill.Blue);
                _body.Append('"');
                if (evenOdd) _body.Append(" fill-rule=\"evenodd\"");
            }
            else
            {
                _body.Append(" fill=\"none\"");
            }
            if (stroke) AppendStroke(_body);
            _body.Append("/>");
        }

        if (_pendingClip is not null && _pathHasPoints)
        {
            string id = NewId("c");
            _defs.Append("<clipPath id=\"").Append(id).Append("\"><path d=\"").Append(_path).Append("\" transform=\"");
            SvgText.AppendMatrix(_defs, _state.Ctm);
            _defs.Append('"');
            if (_pendingClip == "W*") _defs.Append(" clip-rule=\"evenodd\"");
            _defs.Append("/></clipPath>");
            _body.Append("<g clip-path=\"url(#").Append(id).Append(")\">");
            _state.OpenGroups++;
            _paths++;
        }

        _pendingClip = null;
        _path.Clear();
        _pathHasPoints = false;
    }

    private void AppendStroke(StringBuilder builder)
    {
        builder.Append(" stroke=\"");
        SvgText.AppendColor(builder, _state.Stroke.Red, _state.Stroke.Green, _state.Stroke.Blue);
        builder.Append('"');
        if (_state.LineWidth <= 0)
        {
            // A zero width is the thinnest line the device can draw.
            builder.Append(" stroke-width=\"1\" vector-effect=\"non-scaling-stroke\"");
        }
        else
        {
            builder.Append(" stroke-width=\"").Append(SvgText.Number(_state.LineWidth)).Append('"');
        }
        if (_state.LineCap == 1) builder.Append(" stroke-linecap=\"round\"");
        else if (_state.LineCap == 2) builder.Append(" stroke-linecap=\"square\"");
        if (_state.LineJoin == 1) builder.Append(" stroke-linejoin=\"round\"");
        else if (_state.LineJoin == 2) builder.Append(" stroke-linejoin=\"bevel\"");
        else if (_state.MiterLimit != 4 && _state.MiterLimit >= 1) builder.Append(" stroke-miterlimit=\"").Append(SvgText.Number(_state.MiterLimit)).Append('"');
        if (_state.Dash.Length > 0 && _state.Dash.Any(d => d > 0))
        {
            builder.Append(" stroke-dasharray=\"").Append(string.Join(" ", _state.Dash.Select(SvgText.Number))).Append('"');
            if (_state.DashPhase != 0) builder.Append(" stroke-dashoffset=\"").Append(SvgText.Number(_state.DashPhase)).Append('"');
        }
    }

    private void Restore()
    {
        CloseGroups(_state);
        _state = _saved.Pop();
    }

    // Runs one content stream with the given resources.
    private void Run(byte[] content, PdfReadDictionary? resources)
    {
        var reader = new ContentReader(content);
        int baseDepth = _saved.Count;
        while (reader.Next(out string op))
        {
            if (++_operators > _limits.MaxOperators) throw new PageRenderException("The page has too many drawing operators.");
            var operands = reader.Operands;
            switch (op)
            {
                // Graphics state
                case "q": // save
                    if (_saved.Count >= _limits.MaxStateDepth) throw new PageRenderException("The page nests saved states too deeply.");
                    _saved.Push(_state);
                    _state = _state.Copy();
                    _state.OpenGroups = 0;
                    break;
                case "Q": // restore; an unbalanced Q in this stream is ignored
                    if (_saved.Count > baseDepth) Restore();
                    break;
                case "cm": // concatenate matrix
                {
                    var m = Numbers(operands, 6);
                    var ctm = new RenderMatrix(m[0], m[1], m[2], m[3], m[4], m[5]).Then(_state.Ctm);
                    if (!ctm.IsFinite()) throw new PageRenderException("The page has a transformation that is not finite.");
                    _state.Ctm = ctm;
                    break;
                }
                case "w": _state.LineWidth = Numbers(operands, 1)[0]; break; // line width
                case "J": _state.LineCap = (int)Numbers(operands, 1)[0]; break; // line cap
                case "j": _state.LineJoin = (int)Numbers(operands, 1)[0]; break; // line join
                case "M": _state.MiterLimit = Numbers(operands, 1)[0]; break; // miter limit
                case "d": // dash pattern
                    if (operands.Count < 2 || operands[^2] is not PdfReadArray dash) throw new PageRenderException("A dash pattern is not valid.");
                    _state.Dash = dash.Items.Select(item => Math.Abs(Number(_objects.Resolve(item)))).ToArray();
                    _state.DashPhase = Number(operands[^1]);
                    break;
                case "i": case "ri": break; // flatness and rendering intent do not change the drawing

                // Colors in device color spaces
                case "rg": { var c = Numbers(operands, 3); _state.Fill = (c[0], c[1], c[2]); break; }
                case "RG": { var c = Numbers(operands, 3); _state.Stroke = (c[0], c[1], c[2]); break; }
                case "g": { var c = Numbers(operands, 1); _state.Fill = (c[0], c[0], c[0]); break; }
                case "G": { var c = Numbers(operands, 1); _state.Stroke = (c[0], c[0], c[0]); break; }
                case "k": { var c = Numbers(operands, 4); _state.Fill = FromCmyk(c); break; }
                case "K": { var c = Numbers(operands, 4); _state.Stroke = FromCmyk(c); break; }

                // Path construction
                case "m": { var p = Numbers(operands, 2); AddSegment("M", p); break; }
                case "l": { var p = Numbers(operands, 2); AddSegment("L", p); break; }
                case "c": { var p = Numbers(operands, 6); AddSegment("C", p); break; }
                case "v": // curve whose first control point is the current point
                {
                    var p = Numbers(operands, 4);
                    AddSegment("C", _current.X, _current.Y, p[0], p[1], p[2], p[3]);
                    break;
                }
                case "y": // curve whose second control point is its end point
                {
                    var p = Numbers(operands, 4);
                    AddSegment("C", p[0], p[1], p[2], p[3], p[2], p[3]);
                    break;
                }
                case "h": AddSegment("Z"); break; // close subpath
                case "re": // rectangle
                {
                    var r = Numbers(operands, 4);
                    AddSegment("M", r[0], r[1]);
                    AddSegment("L", r[0] + r[2], r[1]);
                    AddSegment("L", r[0] + r[2], r[1] + r[3]);
                    AddSegment("L", r[0], r[1] + r[3]);
                    AddSegment("Z");
                    break;
                }

                // Path painting
                case "f": case "F": Paint(true, false, false); break; // fill
                case "f*": Paint(true, false, true); break; // fill, even-odd
                case "S": Paint(false, true, false); break; // stroke
                case "s": AddSegment("Z"); Paint(false, true, false); break; // close and stroke
                case "B": Paint(true, true, false); break; // fill and stroke
                case "B*": Paint(true, true, true); break;
                case "b": AddSegment("Z"); Paint(true, true, false); break; // close, fill, and stroke
                case "b*": AddSegment("Z"); Paint(true, true, true); break;
                case "n": Paint(false, false, false); break; // end path (applies a pending clip)
                case "W": case "W*": _pendingClip = op; break; // clip with the next painted path

                // Marked content does not change the drawing
                case "BMC": case "MP": case "DP": break;
                case "BDC": BeginMarkedContent(operands, resources); break;
                case "EMC": EndMarkedContent(); break;

                default:
                    if (!RunTextOrObject(op, operands, resources))
                        throw new PageRenderException($"The page uses the operator {op}, which this preview doesn't draw.");
                    break;
            }
        }
        while (_saved.Count > baseDepth) Restore();
    }

    private static (double, double, double) FromCmyk(double[] c) =>
        ((1 - c[0]) * (1 - c[3]), (1 - c[1]) * (1 - c[3]), (1 - c[2]) * (1 - c[3]));

    #endregion

    #endregion
}
