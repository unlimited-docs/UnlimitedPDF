namespace UnlimitedPDF.Models.Internal.Importing;

/// <summary>
/// Reads decoded content and reports each resource name an operator uses, with its resource category: fonts
/// (<c>Tf</c>), external objects (<c>Do</c>), graphics states (<c>gs</c>), color spaces (<c>cs</c>, <c>CS</c>, and
/// inline images), patterns (<c>scn</c>, <c>SCN</c>), shadings (<c>sh</c>), and marked-content properties
/// (<c>BDC</c>, <c>DP</c>). Content holding an inline image is reported as unreadable (WR-040).
/// </summary>
internal static class ContentNameScanner
{
    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns the resource names <paramref name="content"/> uses, in the order its operators use them.
    /// </summary>
    /// <param name="content">The content after its filters are decoded.</param>
    /// <returns>Each use as its resource category and name.</returns>
    /// <exception cref="PdfReadException">The content cannot be read, or holds an inline image.</exception>
    public static List<(string Category, string Name)> Scan(byte[] content)
    {
        var uses = new List<(string Category, string Name)>();
        var lexer = new PdfLexer(content);
        var operands = new List<PdfToken>();
        while (true)
        {
            var token = lexer.Next();
            if (token.Kind == PdfTokenKind.EndOfFile) return uses;
            if (token.Kind != PdfTokenKind.Keyword || token.Text is "true" or "false" or "null")
            {
                operands.Add(token);
                continue;
            }

            // Where inline image data ends cannot be found reliably without decoding it, so content holding one is
            // treated as unreadable and keeps all its resources (WR-040).
            if (token.Text == "BI") throw PdfReadException.Damaged("The content holds an inline image.");
            Record(token.Text!, operands, uses);
            operands.Clear();
        }
    }

    #endregion

    #region Private Methods

    // The name an operator uses, by its position among the operands.
    private static void Record(string op, List<PdfToken> operands, List<(string Category, string Name)> uses)
    {
        static string? NameAt(List<PdfToken> operands, int index) =>
            index >= 0 && index < operands.Count && operands[index].Kind == PdfTokenKind.Name ? operands[index].Text : null;

        (string Category, string? Name) use = op switch
        {
            "Tf" => ("Font", NameAt(operands, 0)),
            "Do" => ("XObject", NameAt(operands, operands.Count - 1)),
            "gs" => ("ExtGState", NameAt(operands, operands.Count - 1)),
            "cs" or "CS" => ("ColorSpace", NameAt(operands, operands.Count - 1)),
            "scn" or "SCN" => ("Pattern", NameAt(operands, operands.Count - 1)),
            "sh" => ("Shading", NameAt(operands, operands.Count - 1)),
            "BDC" or "DP" => ("Properties", operands.Count == 2 ? NameAt(operands, 1) : null),
            _ => ("", null),
        };
        if (use.Name is not null) uses.Add((use.Category, use.Name));
    }

    #endregion

    #endregion
}
