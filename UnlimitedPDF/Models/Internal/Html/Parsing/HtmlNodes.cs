namespace UnlimitedPDF.Models.Internal.Html.Parsing;

/// <summary>
/// A node of the parsed HTML tree.
/// </summary>
internal abstract class HtmlNode
{
    #region Constructors and Finalizers

    protected HtmlNode(int line)
    {
        Line = line;
    }

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the 1-based input line the node starts on.
    /// </summary>
    public int Line { get; }

    /// <summary>
    /// Gets or sets the parent element, or null for the root.
    /// </summary>
    public HtmlElement? Parent { get; set; }

    #endregion
}

/// <summary>
/// A run of text.
/// </summary>
internal sealed class HtmlText : HtmlNode
{
    #region Fields and Constants

    private StringBuilder? _builder;
    private string? _text;

    #endregion

    #region Constructors and Finalizers

    public HtmlText(string text, int line)
        : base(line)
    {
        _text = text;
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Appends text in amortized constant time, so text split into many pieces is still joined in linear time.
    /// </summary>
    /// <param name="text">The text to append.</param>
    public void Append(string text)
    {
        _builder ??= new StringBuilder(_text);
        _builder.Append(text);
        _text = null;
    }

    #endregion

    #endregion

    #region Properties, Indexers

    public string Text
    {
        get => _text ??= _builder!.ToString();
        set
        {
            _text = value;
            _builder = null;
        }
    }

    #endregion
}

/// <summary>
/// An element with a lowercase name, its attributes, and its children.
/// </summary>
internal sealed class HtmlElement : HtmlNode
{
    #region Fields and Constants

    private List<HtmlElement>? _elementChildren;

    #endregion

    #region Constructors and Finalizers

    public HtmlElement(string name, IEnumerable<KeyValuePair<string, string>> attributes, int line)
        : base(line)
    {
        Name = name;
        foreach (var attribute in attributes)
        {
            if (!Attributes.ContainsKey(attribute.Key)) Attributes[attribute.Key] = attribute.Value;
        }
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Appends a child, merging adjacent text.
    /// </summary>
    /// <param name="node">The child.</param>
    public void Append(HtmlNode node)
    {
        if (node is HtmlText text && Children.Count > 0 && Children[Children.Count - 1] is HtmlText last)
        {
            last.Append(text.Text);
            return;
        }
        node.Parent = this;
        _elementChildren = null;
        Children.Add(node);
    }

    /// <summary>
    /// Returns an attribute value, or null.
    /// </summary>
    /// <param name="name">The lowercase name.</param>
    /// <returns>The value, or null.</returns>
    public string? Attribute(string name) => Attributes.TryGetValue(name, out var value) ? value : null;

    /// <summary>
    /// Inserts a child before another child.
    /// </summary>
    /// <param name="node">The new child.</param>
    /// <param name="before">The existing child.</param>
    public void InsertBefore(HtmlNode node, HtmlNode before)
    {
        // Searched from the end: content moved before a table lands just before it, near the end of the list.
        int index = Children.LastIndexOf(before);
        if (index < 0)
        {
            Append(node);
            return;
        }
        if (node is HtmlText text && index > 0 && Children[index - 1] is HtmlText previous)
        {
            previous.Append(text.Text);
            return;
        }
        node.Parent = this;
        _elementChildren = null;
        Children.Insert(index, node);
    }

    /// <summary>
    /// Returns the element's text content: all descendant text in order.
    /// </summary>
    /// <returns>The text.</returns>
    public string TextContent()
    {
        var sb = new StringBuilder();
        var pending = new Stack<HtmlNode>();
        pending.Push(this);
        while (pending.Count > 0)
        {
            var node = pending.Pop();
            if (node is HtmlText text) sb.Append(text.Text);
            else if (node is HtmlElement element)
                for (int i = element.Children.Count - 1; i >= 0; i--) pending.Push(element.Children[i]);
        }
        return sb.ToString();
    }

    #endregion

    #endregion

    #region Properties, Indexers

    public Dictionary<string, string> Attributes { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the child elements in order, computed once after the tree is built (for <c>:first-child</c> and
    /// <c>:last-child</c> in constant time).
    /// </summary>
    public IReadOnlyList<HtmlElement> ElementChildren => _elementChildren ??= Children.OfType<HtmlElement>().ToList();

    public List<HtmlNode> Children { get; } = new();

    public string Name { get; }

    #endregion
}

/// <summary>
/// A stylesheet found in the HTML, in document order: the text of a <c>style</c> element or the reference of a
/// <c>link rel="stylesheet"</c>.
/// </summary>
/// <param name="Text">The CSS text, or null for a link.</param>
/// <param name="Href">The link reference, or null.</param>
/// <param name="Media">The media attribute, or null.</param>
/// <param name="Line">The 1-based line.</param>
internal sealed record HtmlStyleSource(string? Text, string? Href, string? Media, int Line);

/// <summary>
/// The parsed HTML: the root, head, and body elements, the stylesheets, and the title.
/// </summary>
/// <param name="Root">The html element.</param>
/// <param name="Body">The body element.</param>
/// <param name="StyleSources">The stylesheets in document order.</param>
/// <param name="Title">The title text, or null.</param>
internal sealed record HtmlDocumentTree(HtmlElement Root, HtmlElement Body, IReadOnlyList<HtmlStyleSource> StyleSources, string? Title);
