using UnlimitedPDF.Models.Internal.Reading;

namespace UnlimitedPDF.Models.Internal.Rendering;

internal sealed partial class PageInterpreter
{
    #region Fields and Constants

    private readonly List<string> _textLines = new();
    private int _glyphs = 0;
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

    // Form XObjects are drawn from T-06 on; until then a page with one is not drawn.
    private void DrawForm(PdfReadStream form) =>
        throw new PageRenderException("The page draws a form XObject, which this preview doesn't draw yet.");

    // Runs a text or XObject operator; false when the operator is not one of them.
    private bool RunTextOrObject(string op, IReadOnlyList<PdfReadValue> operands, PdfReadDictionary? resources)
    {
        switch (op)
        {
            case "Do": // draw an XObject
                DrawXObject(operands, resources);
                return true;
            default:
                return false;
        }
    }

    #endregion

    #endregion

    #region Properties, Indexers

    private IReadOnlyList<string> TextLines => _textLines;

    #endregion
}
