using UnlimitedPDF.Models.Internal.Reading;

namespace UnlimitedPDF.Models.Internal.Rendering;

/// <summary>
/// Reads a decoded content stream as a sequence of operators, each with the operands written before it.
/// </summary>
internal sealed class ContentReader
{
    #region Fields and Constants

    private readonly PdfLexer _lexer;
    private readonly int _maxOperands;
    private readonly PdfParser _parser;
    private readonly List<PdfReadValue> _operands = new();

    #endregion

    #region Constructors and Finalizers

    /// <summary>
    /// Creates a reader over decoded content bytes.
    /// </summary>
    /// <param name="content">The content stream after its filters are decoded.</param>
    /// <param name="maxOperands">The most operands one operator may have.</param>
    public ContentReader(byte[] content, int maxOperands)
    {
        _maxOperands = maxOperands;
        _lexer = new PdfLexer(content);
        _parser = new PdfParser(_lexer);
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Reads the next operator.
    /// </summary>
    /// <param name="name">The operator, for example <c>re</c> or <c>Tj</c>.</param>
    /// <returns>False at the end of the content.</returns>
    /// <exception cref="PageRenderException">The content is not valid PDF syntax, holds an inline image, or gives an
    /// operator too many operands.</exception>
    public bool Next(out string name)
    {
        _operands.Clear();
        try
        {
            while (true)
            {
                int start = _lexer.Position;
                var token = _lexer.Next();
                if (token.Kind == PdfTokenKind.EndOfFile)
                {
                    name = string.Empty;
                    return false;
                }

                if (token.Kind == PdfTokenKind.Keyword && token.Text is not ("true" or "false" or "null"))
                {
                    // Inline images (BI ... ID ... EI) hold raw bytes that are not tokens; the library never writes them.
                    if (token.Text == "BI") throw new PageRenderException("The page has an inline image.");
                    name = token.Text!;
                    return true;
                }

                if (_operands.Count >= _maxOperands) throw new PageRenderException("An operator on the page has too many operands.");
                _lexer.Position = start;
                _operands.Add(_parser.ParseValue());
            }
        }
        catch (PdfReadException exception)
        {
            throw new PageRenderException($"The page content is damaged: {exception.Message}");
        }
    }

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the operands of the operator <see cref="Next"/> last returned.
    /// </summary>
    public IReadOnlyList<PdfReadValue> Operands => _operands;

    #endregion
}
