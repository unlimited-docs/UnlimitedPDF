using UnlimitedPDF.Models.Internal.Reading;

namespace UnlimitedPDF.Models.Internal.Rendering;

internal sealed partial class PageInterpreter
{
    #region Fields and Constants

    private readonly List<string> _textLines = new();
    private int _glyphs = 0;
    private int _images = 0;
    private int _links = 0;

    #endregion

    #region Methods

    #region Private Methods

    private void BeginMarkedContent(IReadOnlyList<PdfReadValue> operands, PdfReadDictionary? resources)
    {
    }

    private void DrawAnnotations(PdfReadDictionary page, int pageNumber)
    {
    }

    private void EndMarkedContent()
    {
    }

    // Runs a text or XObject operator; false when the operator is not one of them.
    private bool RunTextOrObject(string op, IReadOnlyList<PdfReadValue> operands, PdfReadDictionary? resources) => false;

    #endregion

    #endregion

    #region Properties, Indexers

    private IReadOnlyList<string> TextLines => _textLines;

    #endregion
}
